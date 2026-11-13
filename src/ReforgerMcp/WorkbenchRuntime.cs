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
        JsonElement[]? arguments = null, string returnType = "void", bool details = false)
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
                return new RuntimeResult(state!, process.Id, action == "prepare" && state == "prepared" ? receipt : null,
                    receipt, action is "prepare" or "invoke" or "call" ? returnType : null, returned, attempted,
                    new { prepareMs = activity.PrepareMs, compileMs = activity.CompileMs, invokeMs = activity.InvokeMs, serverMs = activity.ElapsedMs },
                    watch.ElapsedMilliseconds, response,
                    action == "inspect"
                        ? "Live embedded Play observation. Components are ScriptComponent-derived; reflected primitive fields only, not properties/getters or an atomic multi-request snapshot. Unsupported values remain explicit."
                        : "Embedded Workbench Play only. Invoked means the synchronous named call returned true; inspect logs and gameplay state for script exceptions/deferred effects. Return values use the requested compiler-checked type; void explicitly discards results. Never replay an uncertain call.", details);
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

// Kept in the existing bounded operation history. Presentation never calls the engine.
internal sealed record RuntimeResult(string State, int ProcessId, string? TargetToken, string Receipt,
    string? ReturnType, JsonElement? Returned, bool? InvocationAttempted, object Timings,
    long ElapsedMs, JsonElement Adapter, string Scope, bool Details)
{
    internal object Present(string operationId, bool details = false)
    {
        if (details || Details || State != "invoked")
            return new
            {
                state = State, processId = ProcessId, operationId, targetToken = TargetToken, receipt = Receipt,
                returnType = ReturnType, returned = Returned, invocationAttempted = InvocationAttempted,
                timings = Timings, elapsedMs = ElapsedMs, adapter = Adapter, scope = Scope
            };
        return new
        {
            state = State, processId = ProcessId, operationId, receipt = Receipt, returnType = ReturnType,
            returned = Returned, invocationAttempted = InvocationAttempted, timings = Timings,
            target = new
            {
                world = Adapter.GetProperty("world"), entityName = Adapter.GetProperty("entityName"),
                componentType = Adapter.GetProperty("componentType"), method = Adapter.GetProperty("method")
            },
            completion = "synchronous_call_only"
        };
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
    [Description("Read installed adapter readiness and embedded Play state; requires configured runtimePort owned by processId.")]
    public Task<object> Status(int processId, CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("status", processId, null, null, null, null, cancellationToken));

    [McpServerTool(Name = "workbench_runtime_inspect", ReadOnly = true, OpenWorld = false)]
    [Description("Inspect named entity during embedded Play; componentType selects one component for primitive fields, otherwise lists components. Requires installed adapter/runtimePort; separate clients/servers unsupported.")]
    public Task<object> Inspect(int processId, string entityName, string? componentType = null, int offset = 0, int limit = 32,
        CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("inspect", processId, null, entityName, componentType, null, cancellationToken, offset, limit));

    [McpServerTool(Name = "workbench_runtime_call", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Validate and invoke one public component method during embedded Play through the installed adapter/runtimePort. Synchronous return only; no deferred completion or out/inout updates. details adds raw diagnostics; operationId identifies the stored result. Accepted code cannot be cancelled.")]
    public Task<object> Call(int processId, string entityName, string componentType, string method, [Description("All declared arguments, including optional ones; max 9 {type,value} items. type=int|float|bool|string|vector; vector=[x,y,z]. Inputs only.")] JsonElement[]? arguments = null,
        [Description("int|float|bool|string|vector|void; void discards result. Objects/arrays unsupported.")] string returnType = "void", bool details = false, CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("call", processId, null, entityName, componentType, method, cancellationToken, arguments: arguments, returnType: returnType, details: details));

    [McpServerTool(Name = "workbench_runtime_prepare", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Validate a component call without execution during embedded Play through the installed adapter/runtimePort. Returns single-use targetToken valid 55 seconds; any prepare/call supersedes it globally. Reload, world replacement or target destruction invalidates it.")]
    public Task<object> Prepare(int processId, string entityName, string componentType, string method, [Description("All declared arguments, including optional ones; max 9 {type,value} items. type=int|float|bool|string|vector; vector=[x,y,z]. Inputs only.")] JsonElement[]? arguments = null,
        [Description("int|float|bool|string|vector|void; void discards result. Objects/arrays unsupported.")] string returnType = "void", CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("prepare", processId, null, entityName, componentType, method, cancellationToken, arguments: arguments, returnType: returnType));

    [McpServerTool(Name = "workbench_runtime_invoke", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Consume prepared targetToken and invoke once during embedded Play. Synchronous return only; no deferred completion or out/inout updates. Accepted code cannot be cancelled. details adds raw diagnostics; operationId identifies the stored result.")]
    public Task<object> Invoke(string targetToken, bool details = false, CancellationToken cancellationToken = default)
        => Report(() => session.Runtime("invoke", 0, targetToken, null, null, null, cancellationToken, details: details));
}
