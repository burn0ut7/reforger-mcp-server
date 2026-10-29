using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

public sealed partial class WorkbenchSession
{
    private sealed record RuntimePrepared(string Token, int Pid, long Started, DateTime Expires, string ReturnType, RuntimeActivity Activity);
    private RuntimePrepared? runtimePrepared;

    internal Task<object> Runtime(string action, int processId, string? targetToken, string? entityName,
        string? componentType, string? method, CancellationToken ct, int offset = 0, int limit = 32,
        JsonElement[]? arguments = null, string returnType = "void")
    {
        if (action is not ("status" or "prepare" or "invoke" or "inspect" or "call")) throw new ArgumentException("Invalid runtime action.");
        if (action == "inspect" && (string.IsNullOrWhiteSpace(entityName) || Encoding.UTF8.GetByteCount(entityName) > 240 ||
            entityName.Any(char.IsControl) || offset < 0 || offset > int.MaxValue - 64 || limit is < 1 or > 64 ||
            (componentType is not null && !Regex.IsMatch(componentType, "^[A-Za-z_][A-Za-z0-9_]{0,95}$"))))
            throw new ArgumentException("Inspection requires an entity name (at most 240 UTF-8 bytes), optional component type identifier, nonnegative offset and limit 1-64.");
        if (action != "invoke" && processId <= 0) throw new ArgumentException("processId must come from workbench_script_status.");
        if (action is "prepare" or "call")
        {
            if (string.IsNullOrWhiteSpace(entityName) || Encoding.UTF8.GetByteCount(entityName) > 240 || entityName.Any(char.IsControl))
                throw new ArgumentException("entityName must contain 1-240 UTF-8 bytes without control characters.");
            foreach (var identifier in new[] { componentType, method })
                if (identifier is null || !Regex.IsMatch(identifier, "^[A-Za-z_][A-Za-z0-9_]{0,95}$"))
                    throw new ArgumentException("componentType and method must be identifiers of 1-96 ASCII letters, digits or underscores.");
        }
        var callCode = action is "prepare" or "call" ? RuntimeCall.Build(method!, arguments, returnType) : null;
        return Start(token =>
        {
            var port = configuration.Workbench?.RuntimePort ?? throw new InvalidOperationException(
                "Runtime adapter is not configured. Set workbench.runtimePort to the editor port and use workbench_runtime_setup(action=install). No alternate executor is used.");
            using var process = SelectProcess();
            string receipt;
            RuntimeActivity? preparedActivity = null;
            if (action == "invoke")
            {
                var prepared = runtimePrepared;
                if (prepared is null || targetToken != prepared.Token || prepared.Expires <= DateTime.UtcNow)
                    throw new ArgumentException("Unknown, consumed or expired targetToken; prepare the intended call again only after inspecting any uncertain outcome.");
                runtimePrepared = null; // Single use, including transport failure.
                if (process.Id != prepared.Pid || process.StartTime.ToUniversalTime().Ticks != prepared.Started)
                    throw new InvalidOperationException("Workbench process changed; target is stale.");
                receipt = prepared.Token;
                returnType = prepared.ReturnType;
                preparedActivity = prepared.Activity;
            }
            else
            {
                if (process.Id != processId) throw new InvalidOperationException("Workbench process changed.");
                receipt = "r" + Guid.NewGuid().ToString("N");
                if (action is "prepare" or "call") runtimePrepared = null;
            }
            var activity = preparedActivity is null
                ? new RuntimeActivity(action, receipt, entityName ?? "", componentType ?? "", method ?? "", returnType,
                    arguments?.Select(a => a.GetProperty("type").GetString()!).ToArray() ?? [])
                : preparedActivity with { Action = action };
            var watch = Stopwatch.StartNew();
            var trace = action is "prepare" or "invoke" or "call";
            if (trace) ToolActivityLog.RecordRuntime(activity);
            try
            {
                var windows = Windows(process.Id);
                RequiredWindow(windows, ResourceManager);
                if (windows.Any(IsModal) || windows.Any(w => !w.Current.IsEnabled))
                    throw Blocked(process, "Resolve editor dialogs before runtime access.");
                RuntimeNet.VerifyListener(port, process.Id);
                token.ThrowIfCancellationRequested();
                activity = activity with { Dispatch = "sent_outcome_unknown" };
                var response = RuntimeNet.Call(port, new { APIFunc = "RST_Runtime", protocol = 2, action, token = receipt, entityName, componentType, method, offset, limit, callCode });
                if (response.ValueKind != JsonValueKind.Object || !response.TryGetProperty("protocol", out var version) ||
                    version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var protocol) || protocol != 2 ||
                    !response.TryGetProperty("adapterBuild", out var build) || build.ValueKind != JsonValueKind.String || build.GetString() != RuntimeAdapter.Build ||
                    !response.TryGetProperty("token", out var echoed) || echoed.ValueKind != JsonValueKind.String || echoed.GetString() != receipt ||
                    !response.TryGetProperty("state", out var stateValue) || stateValue.ValueKind != JsonValueKind.String)
                    throw new InvalidOperationException("Runtime adapter build/protocol/receipt mismatch; outcome unknown. Do not replay. Inspect workbench_runtime_setup status.");
                var state = stateValue.GetString();
                if (state is not ("playing" or "not_playing" or "prepared" or "entity_missing" or "unsupported_component" or
                    "component_missing_or_ambiguous" or "invalid_request" or "compile_rejected" or "stale_target" or "invoked" or "call_failed" or "incompatible_protocol" or "observed"))
                    throw new InvalidOperationException("Unrecognized runtime response; outcome unknown. Do not replay.");
                if (action == "prepare" && state == "prepared")
                    runtimePrepared = new(receipt, process.Id, process.StartTime.ToUniversalTime().Ticks, DateTime.UtcNow.AddSeconds(55), returnType, activity with { Dispatch = "not_sent" });
                JsonElement? returned = null;
                if (action is "invoke" or "call" && state == "invoked")
                {
                    if (!response.TryGetProperty("resultJson", out var payload) || payload.ValueKind != JsonValueKind.String)
                        throw new InvalidOperationException("Runtime invocation result missing; outcome unknown. Do not replay.");
                    using var parsed = JsonDocument.Parse(payload.GetString()!);
                    if (!parsed.RootElement.TryGetProperty("called", out var called) || called.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new InvalidOperationException("Runtime invocation result malformed; outcome unknown. Do not replay.");
                    if (!called.GetBoolean()) state = "call_failed";
                    else if (returnType != "void") returned = parsed.RootElement.GetProperty("value").Clone();
                }
                int? Timing(string name) => response.TryGetProperty(name, out var value) && value.TryGetInt32(out var ms) && ms >= 0 ? ms : null;
                // JsonApiStruct RegV encodes Enforce bool fields as numeric 0/1.
                bool? attempted = response.TryGetProperty("invocationAttempted", out var attempt)
                    ? attempt.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number when attempt.TryGetInt32(out var flag) && flag is 0 or 1 => flag == 1,
                        _ => throw new InvalidOperationException("Invalid runtime invocation flag; outcome unknown. Do not replay.")
                    } : null;
                activity = activity with { State = state!, Dispatch = "returned", InvocationAttempted = attempted,
                    PrepareMs = Timing("prepareMs"), CompileMs = Timing("compileMs"), InvokeMs = Timing("invokeMs"),
                    ElapsedMs = watch.Elapsed.TotalMilliseconds };
                return new { state, processId = process.Id, targetToken = action == "prepare" && state == "prepared" ? receipt : null,
                    receipt, returnType = action is "prepare" or "invoke" or "call" ? returnType : null, returned,
                    invocationAttempted = attempted,
                    timings = new { prepareMs = activity.PrepareMs, compileMs = activity.CompileMs, invokeMs = activity.InvokeMs, serverMs = activity.ElapsedMs },
                    elapsedMs = watch.ElapsedMilliseconds, adapter = response,
                    scope = action == "inspect"
                        ? "Live embedded Play observation. Components are ScriptComponent-derived; reflected primitive fields only, not properties/getters or an atomic multi-request snapshot. Unsupported values remain explicit."
                        : "Embedded Workbench Play only. Invoked means the synchronous named call returned true; inspect logs and gameplay state for script exceptions/deferred effects. Return values use the requested compiler-checked type; void explicitly discards results. Never replay an uncertain call." };
            }
            catch (Exception ex)
            {
                activity = activity with { State = activity.Dispatch == "sent_outcome_unknown" ? "uncertain" : "rejected",
                    ErrorType = ex.GetType().Name };
                throw;
            }
            finally
            {
                if (trace) ToolActivityLog.RecordRuntime(activity with { ElapsedMs = watch.Elapsed.TotalMilliseconds });
            }
        }, ct);
    }
}

internal static class RuntimeNet
{
    // Verify the configured IPv4 listener belongs to the selected Workbench process.
    // No port discovery, remote host selection, or transport fallback.
    [DllImport("iphlpapi.dll")] private static extern uint GetExtendedTcpTable(nint table, ref int size, bool order, int family, int tableClass, uint reserved);
    internal static void VerifyListener(int port, int pid) => CheckListener(port, pid, false);
    internal static void VerifyPortAvailableOrOwned(int port, int pid) => CheckListener(port, pid, true);
    private static void CheckListener(int port, int pid, bool allowAbsent)
    {
        int size = 0;
        var error = GetExtendedTcpTable(0, ref size, false, 2, 3, 0); // OWNER_PID_LISTENER
        if (error != 122) throw new Win32Exception((int)error);
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            error = GetExtendedTcpTable(memory, ref size, false, 2, 3, 0);
            if (error != 0) throw new Win32Exception((int)error);
            var count = Marshal.ReadInt32(memory);
            if (count < 0 || count > (size - 4) / 24) throw new InvalidDataException("Invalid listener table.");
            var owners = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var row = memory + 4 + i * 24;
                var address = unchecked((uint)Marshal.ReadInt32(row, 4));
                var localPort = ((Marshal.ReadByte(row, 8) << 8) | Marshal.ReadByte(row, 9));
                if (localPort == port && (allowAbsent || address == 0 || address == 0x0100007f)) owners.Add(Marshal.ReadInt32(row, 20));
            }
            if (allowAbsent && owners.Count == 0) return;
            if (owners.Count != 1 || owners[0] != pid) throw new InvalidOperationException(
                $"Configured runtime port {port} is not a unique IPv4 listener owned by Workbench PID {pid}. Observed owners: [{string.Join(", ", owners)}].");
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

    internal static JsonElement Call(int port, object request)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new TcpClient(AddressFamily.InterNetwork);
        bool dispatched = false;
        try
        {
            client.ConnectAsync(IPAddress.Loopback, port, deadline.Token).AsTask().GetAwaiter().GetResult();
            using var stream = client.GetStream();
            using var message = new MemoryStream();
            using (var writer = new BinaryWriter(message, Encoding.UTF8, true))
            {
                writer.Write(1);
                foreach (var value in new[] { "ReforgerMcp", "JsonRPC", JsonSerializer.Serialize(request) })
                { var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
            }
            dispatched = true;
            stream.WriteAsync(message.ToArray(), deadline.Token).AsTask().GetAwaiter().GetResult();
            string Read()
            {
                var sizeBytes = new byte[4];
                stream.ReadExactlyAsync(sizeBytes, deadline.Token).AsTask().GetAwaiter().GetResult();
                var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBytes);
                if (size is < 0 or > 65536) throw new InvalidDataException("Runtime response exceeds 64 KiB.");
                var bytes = new byte[size]; stream.ReadExactlyAsync(bytes, deadline.Token).AsTask().GetAwaiter().GetResult();
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            var code = Read(); var payload = Read();
            if (code != "Ok") throw new InvalidOperationException("Runtime adapter rejected request: " + code);
            using var parsed = JsonDocument.Parse(payload);
            return parsed.RootElement.Clone();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(dispatched
                ? "Runtime request outcome unknown or rejected after dispatch; inspect logs before any further call. No automatic retry. " + ex.Message
                : "Runtime unavailable before dispatch. " + ex.Message, ex);
        }
    }
}

[McpServerToolType]
public sealed class WorkbenchRuntimeTools(WorkbenchSession session)
{
    private static async Task<object> Report(Func<Task<object>> operation)
    {
        try { return await operation(); }
        catch (Exception e) when (e is not (McpException or OperationCanceledException)) { throw new McpException(e.Message); }
    }
    [McpServerTool(Name = "workbench_runtime_status", ReadOnly = true, OpenWorld = false)]
    [Description("Observe the explicitly installed Workbench runtime adapter and embedded Play state. Requires workbench.runtimePort and processId. The configured local listener must belong to that process. No native VM or transport fallback. Pending uses workbench_operation.")]
    public Task<object> Status(int processId, CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("status", processId, null, null, null, null, cancellationToken));

    [McpServerTool(Name = "workbench_runtime_inspect", ReadOnly = true, OpenWorld = false)]
    [Description("Observe a named live entity during embedded Play: identity, transform and a page of script components; supply componentType to read a page of reflected fields on exactly one matching component. Primitive int/float/bool/string/vector values are read without calling gameplay methods. Unsupported field types and truncation are explicit. Does not inspect editor authoring values or separate clients/servers. Requires the installed development adapter and configured runtimePort; pending uses workbench_operation.")]
    public Task<object> Inspect(int processId, string entityName, string? componentType = null, int offset = 0, int limit = 32,
        CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("inspect", processId, null, entityName, componentType, null, cancellationToken, offset, limit));

    [McpServerTool(Name = "workbench_runtime_call", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Call a public component method during embedded Workbench Play. Preferred for immediate execution: resolves the named entity and unique matching component, compiler-checks the typed signature, then invokes once in one adapter request. Uses the same executor and checks as prepare/invoke; no preparation token or separate invoke needed. arguments contains at most nine {type,value} items (int, float, bool, string, vector); supply every declared argument. returnType selects a supported primitive result or void to discard it. Returns receipt, typed result, invocationAttempted and phase timings. invoked means the synchronous call returned, not proof of error-free gameplay or deferred completion; inspect receipt-correlated logs and relevant state. No automatic retries. Timeout/disconnection is uncertain; engine code cannot be cancelled after dispatch. Poll pending workbench_operation. Requires the matching installed development adapter. Separate clients/servers, object/array results and out/inout updates are unsupported. Target/method names and argument types are logged; argument values/results are omitted.")]
    public Task<object> Call(int processId, string entityName, string componentType, string method, JsonElement[]? arguments = null,
        string returnType = "void", CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("call", processId, null, entityName, componentType, method, cancellationToken, arguments: arguments, returnType: returnType));

    [McpServerTool(Name = "workbench_runtime_prepare", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("For validation without execution or an intentional two-step call. Prefer workbench_runtime_call for immediate execution. During embedded Play, resolve the engine's named entity and exactly one component assignable to componentType, then compile-check a public method against the supplied typed signature without invoking it. arguments is an array of at most nine {type,value} objects; types are int, float, bool, string and vector ([x,y,z]). returnType is one of those types or void (default). Supply all declared arguments, including optional ones. Returns a single-use targetToken valid for 55 seconds on this connection. Any prepare replaces the adapter's previous prepared call globally. Object destruction, reload and world replacement invalidate it. Requires explicit development adapter installation and workbench.runtimePort. Invocation returns the requested supported value; void discards the result. Object references and arrays are unsupported. Arguments are inputs only: Enforce may accept out/inout methods, but argument updates are not returned. Pending uses workbench_operation.")]
    public Task<object> Prepare(int processId, string entityName, string componentType, string method, JsonElement[]? arguments = null,
        string returnType = "void", CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("prepare", processId, null, entityName, componentType, method, cancellationToken, arguments: arguments, returnType: returnType));

    [McpServerTool(Name = "workbench_runtime_invoke", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Consume a prepared live-component targetToken and synchronously call its method through the owning game ScriptModule in embedded Workbench Play. Runs arbitrary project behavior with its normal authority; does not emulate player interaction or await deferred effects. Receipt brackets RST_MCP_BEGIN/END in console.log. invoked means ScriptModule.Call returned true, not proof that the method finished without script errors or that gameplay assertions passed. No retries. Timeout/disconnection is uncertain; cancellation after acceptance cannot cancel engine code. Poll pending with workbench_operation on this connection, inspect logs after uncertainty. Returns the compiler-checked primitive result selected at prepare time. Separate game clients/servers are unsupported.")]
    public Task<object> Invoke(string targetToken, CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("invoke", 0, targetToken, null, null, null, cancellationToken));
}
