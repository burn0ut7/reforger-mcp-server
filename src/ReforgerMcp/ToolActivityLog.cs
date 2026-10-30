using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

// Deliberately excludes argument values, results, generated source and error text.
public sealed record RuntimeActivity(string Action, string Receipt, string EntityName, string ComponentType,
    string Method, string ReturnType, string[] ArgumentTypes)
{
    public string State { get; init; } = "started";
    public string Dispatch { get; init; } = "not_sent";
    public string? ErrorType { get; init; }
    public bool? InvocationAttempted { get; init; }
    public int? PrepareMs { get; init; }
    public int? CompileMs { get; init; }
    public int? InvokeMs { get; init; }
    public double ElapsedMs { get; init; }
}

/// <summary>One bounded log per stdio connection or stateless HTTP host run.</summary>
public sealed class ToolActivityLog : IDisposable
{
    public const int MaxBytes = 1_000_000;
    private const int SummaryReserve = 96_000;
    private readonly object gate = new();
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly string session = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, long> usage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResponseTotals> responseUsage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TimingTotals> timingUsage = new(StringComparer.Ordinal);
    private sealed class TimingTotals
    {
        public long Completed { get; set; }
        public long Failures { get; set; }
        public double TotalMs { get; set; }
        public double MaxMs { get; set; }
    }
    private readonly record struct ResponseSize(long TextBytes, long StructuredBytes, int NonTextBlocks);
    private sealed class ResponseTotals
    {
        public long Responses { get; set; }
        public long TextBytes { get; set; }
        public long MaxTextBytes { get; set; }
        public long StructuredBytes { get; set; }
        public long NonTextBlocks { get; set; }
    }
    private static readonly AsyncLocal<Action<RuntimeActivity>?> runtimeObserver = new();
    private readonly Dictionary<string, RuntimeTotals> runtimeUsage = new(StringComparer.Ordinal);
    private sealed class RuntimeTotals
    {
        public long Count { get; set; }
        public long Invoked { get; set; }
        public long Prepared { get; set; }
        public long Failed { get; set; }
        public long Uncertain { get; set; }
        public long Timed { get; set; }
        public long PrepareMs { get; set; }
        public long CompileMs { get; set; }
        public long InvokeMs { get; set; }
        public double ElapsedMs { get; set; }
    }

    // ExecutionContext carries the originating call into pending Workbench work.
    // Polling the result never records a second execution or attributes it to the poll.
    public static void RecordRuntime(RuntimeActivity activity) => runtimeObserver.Value?.Invoke(activity);
    private FileStream? stream;
    private long detailEnd, calls, completed, failures, omitted;
    private double totalMs, slowestMs;
    private string slowestTool = "none";
    private long slowestCall;
    private bool closed, warned;

    public ToolActivityLog(string directory, string transport)
    {
        TryLog(() =>
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"session-{DateTime.UtcNow:yyyyMMddTHHmmssfffffff}-{session}.log");
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            Detail($"SESSION transport={transport} scope={(transport == "stdio" ? "connection" : "host-run")} server_mvid={typeof(ToolActivityLog).Assembly.ManifestModule.ModuleVersionId} adapter_build={RuntimeAdapter.Build}");
            Summary(false);
            foreach (var old in Directory.GetFiles(directory, "session-*.log").OrderDescending(StringComparer.Ordinal).Skip(5))
                File.Delete(old);
        });
    }

    public async ValueTask<CallToolResult> Invoke(RequestContext<CallToolRequestParams> request,
        McpRequestHandler<CallToolRequestParams, CallToolResult> next, CancellationToken cancellationToken)
    {
        var tool = Label(request.Params?.Name ?? "unknown");
        var usageKey = tool;
        long id = 0;
        TryLog(() =>
        {
            if (closed) return;
            id = ++calls;
            usageKey = usage.ContainsKey(tool) || usage.Count < 128 ? tool : "other";
            usage[usageKey] = usage.GetValueOrDefault(usageKey) + 1;
            Detail($"START call={id} tool={tool} params={SafeParameters(request.Params?.Arguments, tool)}");
            Summary(false);
        });
        var start = Stopwatch.GetTimestamp();
        var error = "none";
        ResponseSize? responseSize = null;
        var previousObserver = runtimeObserver.Value;
        runtimeObserver.Value = activity => Runtime(id, activity);
        try
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            if (result.IsError == true) error = "tool-reported-error (body omitted)";
            // Count the actual SDK text payload outside the log lock. No payload is
            // retained, and images/audio are never decoded or copied for telemetry.
            try
            {
                long textBytes = 0;
                var nonTextBlocks = 0;
                foreach (var block in result.Content)
                    if (block is TextContentBlock text) textBytes += Encoding.UTF8.GetByteCount(text.Text);
                    else nonTextBlocks++;
                var structuredBytes = result.StructuredContent is { } structured
                    ? Encoding.UTF8.GetByteCount(structured.GetRawText()) : 0;
                responseSize = new(textBytes, structuredBytes, nonTextBlocks);
            }
            catch (Exception) { /* Measurement must never replace a tool result. */ }
            return result;
        }
        catch (Exception ex)
        {
            error = ex is OperationCanceledException ? "cancelled" : Label(ex.GetType().Name) + " (message omitted)";
            throw;
        }
        finally
        {
            runtimeObserver.Value = previousObserver;
            var duration = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            TryLog(() =>
            {
                if (closed) return;
                completed++;
                if (error != "none") failures++;
                totalMs += duration;
                if (!timingUsage.TryGetValue(usageKey, out var timing)) timingUsage[usageKey] = timing = new();
                timing.Completed++;
                if (error != "none") timing.Failures++;
                timing.TotalMs = Math.Round(timing.TotalMs + duration, 1);
                timing.MaxMs = Math.Max(timing.MaxMs, Math.Round(duration, 1));
                if (duration >= slowestMs) { slowestMs = duration; slowestTool = tool; slowestCall = id; }
                if (responseSize is { } size)
                {
                    if (!responseUsage.TryGetValue(usageKey, out var totals)) responseUsage[usageKey] = totals = new();
                    totals.Responses++;
                    totals.TextBytes += size.TextBytes;
                    totals.MaxTextBytes = Math.Max(totals.MaxTextBytes, size.TextBytes);
                    totals.StructuredBytes += size.StructuredBytes;
                    totals.NonTextBlocks += size.NonTextBlocks;
                }
                Detail($"{(error == "none" ? "OK" : "FAIL")} call={id} tool={tool} duration_ms={Number(duration)} error={error}" +
                    $" response_text_bytes={responseSize?.TextBytes.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}" +
                    $" response_structured_bytes={responseSize?.StructuredBytes.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}" +
                    $" response_nontext_blocks={responseSize?.NonTextBlocks.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}");
                Summary(false);
            });
        }
    }

    // Default-deny string/object contents, including unknown future tool parameters.
    // Only known non-secret scalar controls are printed verbatim.
    public static string SafeParameters(IDictionary<string, JsonElement>? arguments, string? tool = null)
    {
        if (arguments is null) return "{}";
        var safe = new Dictionary<string, object>();
        foreach (var pair in arguments.Take(16))
        {
            var key = Label(pair.Key);
            var scalar = pair.Key is "limit" or "offset" or "maxResults" or "maxLines" or "line" or "count" or "timeoutMs" or "recursive" or "dryRun" or "details";
            var runtimeName = tool is "workbench_runtime_call" or "workbench_runtime_prepare" &&
                pair.Key is "entityName" or "componentType" or "method" or "returnType";
            safe[key] = runtimeName && pair.Value.ValueKind == JsonValueKind.String
                ? pair.Value.GetString()![..Math.Min(pair.Value.GetString()!.Length, 240)]
                : scalar && pair.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? pair.Value.ToString()[..Math.Min(pair.Value.ToString().Length, 32)]
                : "[redacted:" + pair.Value.ValueKind + "]";
        }
        if (arguments.Count > 16) safe["..."] = "remaining parameters omitted";
        return JsonSerializer.Serialize(safe);
    }

    private void Runtime(long callId, RuntimeActivity activity) => TryLog(() =>
    {
        if (closed) return;
        static string Bound(string value, int length) => value[..Math.Min(value.Length, length)];
        var safe = activity with
        {
            Action = Label(activity.Action), Receipt = Label(activity.Receipt),
            EntityName = Bound(activity.EntityName, 240), ComponentType = Bound(activity.ComponentType, 96),
            Method = Bound(activity.Method, 96), ReturnType = Label(activity.ReturnType),
            ArgumentTypes = activity.ArgumentTypes.Take(9).Select(Label).ToArray(),
            State = Label(activity.State), Dispatch = Label(activity.Dispatch),
            ErrorType = activity.ErrorType is null ? null : Label(activity.ErrorType)
        };
        Detail($"RUNTIME call={callId} data={JsonSerializer.Serialize(safe)}");
        if (safe.State != "started")
        {
            var key = safe.Action + " " + safe.ComponentType + "." + safe.Method;
            if (!runtimeUsage.ContainsKey(key) && runtimeUsage.Count >= 32) key = "other";
            if (!runtimeUsage.TryGetValue(key, out var totals)) runtimeUsage[key] = totals = new();
            totals.Count++;
            if (safe.State == "invoked") totals.Invoked++;
            else if (safe.State == "prepared") totals.Prepared++;
            else if (safe.Dispatch == "sent_outcome_unknown") totals.Uncertain++;
            else totals.Failed++;
            if (safe.PrepareMs is >= 0 || safe.InvokeMs is >= 0) totals.Timed++;
            totals.PrepareMs += Math.Max(0, safe.PrepareMs ?? 0);
            totals.CompileMs += Math.Max(0, safe.CompileMs ?? 0);
            totals.InvokeMs += Math.Max(0, safe.InvokeMs ?? 0);
            totals.ElapsedMs += safe.ElapsedMs;
        }
        Summary(false);
    });

    private static string Label(string value) => new(value.Take(64).Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_').ToArray());
    private static string Number(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
    private string Line(string text) => $"{DateTime.UtcNow:O} session={session} {text}\n";

    private void Detail(string text)
    {
        if (stream is null) return;
        var bytes = Encoding.UTF8.GetBytes(Line(text));
        if (detailEnd + bytes.Length > MaxBytes - SummaryReserve) { omitted++; return; }
        stream.Position = detailEnd;
        stream.Write(bytes);
        detailEnd = stream.Position;
    }

    // Replace the footer, so long-lived sessions retain current totals after the detail cap.
    private void Summary(bool final)
    {
        if (stream is null) return;
        var text = Line($"SUMMARY state={(final ? "ended" : "active")} calls={calls} completed={completed} failures={failures} in_flight={calls - completed} elapsed_ms={Number(Stopwatch.GetElapsedTime(started).TotalMilliseconds)} accumulated_tool_ms={Number(totalMs)} slowest_call={slowestCall} slowest_tool={slowestTool} slowest_ms={Number(slowestMs)} omitted_detail_lines={omitted} per_tool={JsonSerializer.Serialize(usage)} timing_per_tool={JsonSerializer.Serialize(timingUsage)} runtime_per_method={JsonSerializer.Serialize(runtimeUsage)} response_per_tool={JsonSerializer.Serialize(responseUsage)}");
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Position = detailEnd;
        stream.Write(bytes.AsSpan(0, Math.Min(bytes.Length, SummaryReserve)));
        stream.SetLength(stream.Position);
        stream.Flush();
    }

    private void TryLog(Action action)
    {
        lock (gate)
        {
            try { action(); }
            catch (Exception)
            {
                // Disk/serialization failures must never change the tool result or exception.
                if (!warned)
                {
                    warned = true;
                    try { Console.Error.WriteLine("MCP activity logging unavailable; tool execution continues."); } catch (Exception) { }
                }
            }
        }
    }

    public void Dispose()
    {
        TryLog(() =>
        {
            if (closed) return;
            closed = true;
            try { Summary(true); }
            finally { stream?.Dispose(); stream = null; }
        });
    }
}
