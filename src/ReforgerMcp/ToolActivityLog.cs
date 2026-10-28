using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

/// <summary>One bounded log per stdio connection or stateless HTTP host run.</summary>
public sealed class ToolActivityLog : IDisposable
{
    public const int MaxBytes = 1_000_000;
    private const int SummaryReserve = 32_000;
    private readonly object gate = new();
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly string session = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, long> usage = new(StringComparer.Ordinal);
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
            Detail($"SESSION transport={transport} scope={(transport == "stdio" ? "connection" : "host-run")}");
            Summary(false);
            foreach (var old in Directory.GetFiles(directory, "session-*.log").OrderDescending(StringComparer.Ordinal).Skip(5))
                File.Delete(old);
        });
    }

    public async ValueTask<CallToolResult> Invoke(RequestContext<CallToolRequestParams> request,
        McpRequestHandler<CallToolRequestParams, CallToolResult> next, CancellationToken cancellationToken)
    {
        var tool = Label(request.Params?.Name ?? "unknown");
        long id = 0;
        TryLog(() =>
        {
            if (closed) return;
            id = ++calls;
            var key = usage.ContainsKey(tool) || usage.Count < 128 ? tool : "other";
            usage[key] = usage.GetValueOrDefault(key) + 1;
            Detail($"START call={id} tool={tool} params={SafeParameters(request.Params?.Arguments)}");
            Summary(false);
        });
        var start = Stopwatch.GetTimestamp();
        var error = "none";
        try
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            if (result.IsError == true) error = "tool-reported-error (body omitted)";
            return result;
        }
        catch (Exception ex)
        {
            error = ex is OperationCanceledException ? "cancelled" : Label(ex.GetType().Name) + " (message omitted)";
            throw;
        }
        finally
        {
            var duration = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            TryLog(() =>
            {
                if (closed) return;
                completed++;
                if (error != "none") failures++;
                totalMs += duration;
                if (duration >= slowestMs) { slowestMs = duration; slowestTool = tool; slowestCall = id; }
                Detail($"{(error == "none" ? "OK" : "FAIL")} call={id} tool={tool} duration_ms={Number(duration)} error={error}");
                Summary(false);
            });
        }
    }

    // Default-deny string/object contents, including unknown future tool parameters.
    // Only known non-secret scalar controls are printed verbatim.
    public static string SafeParameters(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null) return "{}";
        var safe = new Dictionary<string, object>();
        foreach (var pair in arguments.Take(16))
        {
            var key = Label(pair.Key);
            var scalar = pair.Key is "limit" or "offset" or "maxResults" or "maxLines" or "line" or "count" or "timeoutMs" or "recursive" or "dryRun";
            safe[key] = scalar && pair.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? pair.Value.ToString()[..Math.Min(pair.Value.ToString().Length, 32)]
                : "[redacted:" + pair.Value.ValueKind + "]";
        }
        if (arguments.Count > 16) safe["..."] = "remaining parameters omitted";
        return JsonSerializer.Serialize(safe);
    }

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
        var text = Line($"SUMMARY state={(final ? "ended" : "active")} calls={calls} completed={completed} failures={failures} in_flight={calls - completed} elapsed_ms={Number(Stopwatch.GetElapsedTime(started).TotalMilliseconds)} accumulated_tool_ms={Number(totalMs)} slowest_call={slowestCall} slowest_tool={slowestTool} slowest_ms={Number(slowestMs)} omitted_detail_lines={omitted} per_tool={JsonSerializer.Serialize(usage)}");
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
