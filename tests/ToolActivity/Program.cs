using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ReforgerMcp;

var root = Path.GetFullPath(Path.Combine(".temp", "tool-activity-tests", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(root);
await using var server = McpServer.Create(new StreamServerTransport(new MemoryStream(), new MemoryStream()), new McpServerOptions());
RequestContext<CallToolRequestParams> Request(string name = "test") => new(server,
    new JsonRpcRequest { Id = new RequestId(1), Method = "tools/call" },
    new CallToolRequestParams { Name = name, Arguments = new Dictionary<string, JsonElement>
    {
        ["password"] = JsonSerializer.SerializeToElement("NEVER-LOG-THIS"),
        ["content"] = JsonSerializer.SerializeToElement(new { nested = "NEVER-LOG-THIS" }),
        ["limit"] = JsonSerializer.SerializeToElement(12)
    } });
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
JsonElement Presented(object result, bool completed, string? known = null, bool details = false) =>
    JsonSerializer.SerializeToElement(OperationPresentation.Present(result, "test-operation", completed,
        "workbench_operation", known, details), Configuration.Json);
object Pending(string button, string observed, string expires) => new
{
    state = "pending", dialogInspection = new
    {
        state = "observed", observedAt = observed, expiresAt = expires,
        windows = new[] { new { buttonId = button, text = "Save changes?", expiresAt = expires } }
    }
};
var firstPoll = Presented(Pending("save-1", "now", "later"), false);
var pollRevision = firstPoll.GetProperty("revision").GetString()!;
var repeatPoll = Presented(Pending("save-1", "next", "later"), false, pollRevision);
Check(repeatPoll.GetProperty("unchanged").GetBoolean() &&
    !repeatPoll.TryGetProperty("dialogInspection", out _) &&
    repeatPoll.GetProperty("poll").GetProperty("arguments").GetProperty("knownRevision").GetString() == pollRevision,
    "unchanged poll omits repeated dialog and carries caller revision");
Check(Presented(Pending("save-2", "next", "later-2"), false, pollRevision).TryGetProperty("dialogInspection", out _),
    "changed or expired dialog selections are delivered");
Check(Presented(Pending("save-1", "next", "later"), false).TryGetProperty("dialogInspection", out _) &&
    Presented(Pending("save-1", "next", "later"), false, pollRevision, true).TryGetProperty("dialogInspection", out _),
    "other callers and explicit details retain full observation");
object EmptyPending(string time) => new { state = "pending", dialogInspection = new
    { state = "observed", observedAt = time, expiresAt = time, windows = Array.Empty<object>(), disabledEditors = 0 } };
var emptyPoll = Presented(EmptyPending("now"), false);
Check(Presented(EmptyPending("later"), false, emptyPoll.GetProperty("revision").GetString()).GetProperty("unchanged").GetBoolean(),
    "empty window observation timestamps do not create false progress");
var operationFailure = new { state = "reload_failed", diagnostics = new { errors = new[] { "compile error" } } };
var finalPoll = Presented(operationFailure, true, pollRevision);
Check(finalPoll.GetProperty("operationCompleted").GetBoolean() && finalPoll.TryGetProperty("diagnostics", out _),
    "completion delivers original failure diagnostics");
var finalRevision = finalPoll.GetProperty("revision").GetString();
Check(Presented(operationFailure, true, finalRevision).GetProperty("unchanged").GetBoolean() &&
    Presented(operationFailure, true, finalRevision, true).TryGetProperty("diagnostics", out _) &&
    !Presented(operationFailure, true, finalRevision).TryGetProperty("poll", out _),
    "completed result deduplicates only for caller acknowledgement and details restores it");
var largeResult = new { state = "reloaded", completed = 2, diagnostics = new { output = new string('x', 8000) } };
var fullPoll = Presented(largeResult, true);
Check(fullPoll.GetProperty("completed").GetInt32() == 2 && fullPoll.GetProperty("operationCompleted").GetBoolean(),
    "poll completion does not overwrite native dispatch counters");
var shortPoll = Presented(largeResult, true, fullPoll.GetProperty("revision").GetString());
Check(JsonSerializer.SerializeToUtf8Bytes(shortPoll).Length < JsonSerializer.SerializeToUtf8Bytes(fullPoll).Length / 10,
    "acknowledged large result shrinks by more than 90 percent");
string ReadLog(string path) { using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); using var reader = new StreamReader(input); return reader.ReadToEnd(); }
var dir = Path.Combine(root, "unit");
var log = new ToolActivityLog(dir, "stdio");
var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var pending = Enumerable.Range(0, 20).Select(_ => log.Invoke(Request(), async (_, _) =>
{
    await release.Task;
    return new CallToolResult();
}, default).AsTask()).ToArray();
Check(ReadLog(Directory.GetFiles(dir).Single()).Contains("in_flight=20"), "concurrent calls start independently");
release.SetResult();
await Task.WhenAll(pending);
var expected = new InvalidOperationException("NEVER-LOG-THIS");
try { await log.Invoke(Request(), (_, _) => throw expected, default); }
catch (Exception ex) { Check(ReferenceEquals(ex, expected), "exception identity preserved"); }
try { await log.Invoke(Request(), (_, _) => throw new OperationCanceledException("NEVER-LOG-THIS"), default); }
catch (OperationCanceledException) { }
var failure = new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "NEVER-LOG-THIS" }] };
Check(ReferenceEquals(await log.Invoke(Request(), (_, _) => ValueTask.FromResult(failure), default), failure), "error result identity preserved");
log.Dispose();
log.Dispose();
var text = ReadLog(Directory.GetFiles(dir).Single());
Check(text.Contains("state=ended calls=23 completed=23 failures=3 in_flight=0"), "final counts and idempotent disposal");
Check(!text.Contains("NEVER-LOG-THIS") && text.Contains("redacted") && text.Contains("12"), "parameters and errors redacted");
Check(text.Contains("accumulated_tool_ms=") && text.Contains("elapsed_ms=") && text.Contains("slowest_call="), "timing summary fields");
Check(text.Split('\n').Count(l => l.Contains("START call=")) == 23, "all starts present");

var runtimeDir = Path.Combine(root, "runtime");
using (var runtimeLog = new ToolActivityLog(runtimeDir, "http-stateless"))
{
    var activity = new RuntimeActivity("call", "rtest", "Test\nEntity", "TestComponent", "ReadState", "string", ["string"]);
    var request = Request("workbench_runtime_call");
    request.Params!.Arguments!["method"] = JsonSerializer.SerializeToElement("ReadState");
    request.Params.Arguments["arguments"] = JsonSerializer.SerializeToElement(new[] { new { type = "string", value = "NEVER-LOG-THIS" } });
    var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Task? background = null;
    await runtimeLog.Invoke(request, (_, _) =>
    {
        ToolActivityLog.RecordRuntime(activity);
        background = Task.Run(async () =>
        {
            await complete.Task;
            ToolActivityLog.RecordRuntime(activity with { State = "invoked", Dispatch = "returned", InvocationAttempted = true,
                PrepareMs = 4, CompileMs = 3, InvokeMs = 2, ElapsedMs = 12 });
        });
        return ValueTask.FromResult(new CallToolResult());
    }, default);
    await runtimeLog.Invoke(Request("workbench_operation"), (_, _) => ValueTask.FromResult(new CallToolResult()), default);
    complete.SetResult();
    await background!;
    text = ReadLog(Directory.GetFiles(runtimeDir).Single());
    Check(text.Split('\n').Count(l => l.Contains("RUNTIME call=1 ")) == 2 && !text.Contains("RUNTIME call=2 "), "pending runtime completion retains originating call ID");
    Check(text.Contains("ReadState") && text.Contains("Test\\nEntity") && !text.Contains("NEVER-LOG-THIS"), "runtime identities logged, newlines escaped and values redacted");
    Check(text.Contains("\"CompileMs\":3") && text.Contains("\"Invoked\":1"), "runtime phase timings and method totals");
    Check(text.Contains("server_mvid=") && text.Contains("adapter_build="), "build provenance recorded");
    for (var i = 0; i < 140; i++)
        await runtimeLog.Invoke(Request("tool_" + i + new string('x', 60)), (_, _) =>
        {
            ToolActivityLog.RecordRuntime(activity with { ComponentType = new string('C', 96), Method = "Method" + i + new string('M', 85), State = "uncertain", Dispatch = "sent_outcome_unknown" });
            return ValueTask.FromResult(new CallToolResult());
        }, default);
    text = ReadLog(Directory.GetFiles(runtimeDir).Single());
    var footer = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Last();
    var runtimeTotalsJson = footer.Split("runtime_per_method=")[1].Split(" response_per_tool=")[0];
    using var totals = JsonDocument.Parse(runtimeTotalsJson);
    Check(totals.RootElement.EnumerateObject().Count() == 33 && totals.RootElement.GetProperty("other").GetProperty("Uncertain").GetInt64() > 0, "runtime method cardinality bounded with explicit overflow");
}

var sizesDir = Path.Combine(root, "response-sizes");
using (var sizes = new ToolActivityLog(sizesDir, "stdio"))
{
    var result = new CallToolResult
    {
        Content = [new TextContentBlock { Text = "é😀" }, new ImageContentBlock { MimeType = "image/png", Data = Encoding.UTF8.GetBytes("not-decoded") }],
        StructuredContent = JsonSerializer.SerializeToElement(new { message = "NEVER-LOG-THIS" })
    };
    Check(ReferenceEquals(await sizes.Invoke(Request("workbench_runtime_call"), (_, _) => ValueTask.FromResult(result), default), result), "response measurement preserves original result");
    await sizes.Invoke(Request("workbench_operation"), (_, _) => ValueTask.FromResult(result), default);
    await sizes.Invoke(Request("workbench_operation"), (_, _) => ValueTask.FromResult(failure), default);
    try { await sizes.Invoke(Request("throws"), (_, _) => throw expected, default); } catch (InvalidOperationException) { }
    text = ReadLog(Directory.GetFiles(sizesDir).Single());
    Check(text.Contains("response_text_bytes=6 ") && text.Contains("response_nontext_blocks=1"), "counts UTF-8 bytes and non-text blocks without decoding media");
    Check(!text.Contains("NEVER-LOG-THIS") && !text.Contains("not-decoded"), "response metrics never log content");
    using var sizeTotals = JsonDocument.Parse(text.Split("response_per_tool=").Last().Trim());
    Check(sizeTotals.RootElement.GetProperty("workbench_runtime_call").GetProperty("TextBytes").GetInt64() == 6 &&
        sizeTotals.RootElement.GetProperty("workbench_operation").GetProperty("Responses").GetInt64() == 2,
        "poll response volume accounted separately from runtime execution");
    Check(sizeTotals.RootElement.GetProperty("workbench_runtime_call").GetProperty("StructuredBytes").GetInt64() ==
        Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText()), "structured JSON measured separately");
    Check(!sizeTotals.RootElement.TryGetProperty("throws", out _) && text.Contains("response_text_bytes=unavailable"), "thrown exceptions do not invent response sizes");
}

// Successful projection must preserve all returned content; detail retrieval only
// changes presentation of this original object, never validation or execution.
var longValue = string.Concat(Enumerable.Repeat("é\"\\\n", 3000));
var adapter = JsonSerializer.SerializeToElement(new
{
    world = "test-world", entityName = "entity", componentType = "Component", method = "Method",
    resultJson = JsonSerializer.Serialize(new { called = true, value = longValue }),
    error = "", errorLine = 0
});
var stored = new RuntimeResult("invoked", 123, null, "receipt", "string", JsonSerializer.SerializeToElement(longValue), true,
    new { prepareMs = 1, compileMs = 1, invokeMs = 2, serverMs = 10 }, 10, adapter, "scope", false);
var compact = JsonSerializer.SerializeToElement(stored.Present("operation"));
var detailed = JsonSerializer.SerializeToElement(stored.Present("operation", true));
Check(compact.GetProperty("returned").GetString() == longValue && !compact.TryGetProperty("adapter", out _), "compact response retains full Unicode/escaped long result once");
Check(compact.GetProperty("target").GetProperty("method").GetString() == "Method" &&
    compact.GetProperty("completion").GetString() == "synchronous_call_only", "compact identity and completion semantics preserved");
Check(detailed.GetProperty("adapter").GetProperty("resultJson").GetString() == adapter.GetProperty("resultJson").GetString() &&
    detailed.GetProperty("receipt").GetString() == compact.GetProperty("receipt").GetString(), "detailed projection preserves original diagnostics and receipt");
Check(JsonSerializer.SerializeToElement((stored with { Details = true }).Present("operation")).TryGetProperty("adapter", out _), "explicit details survives operation polling");
Check(JsonSerializer.SerializeToElement((stored with { State = "compile_rejected" }).Present("operation")).TryGetProperty("adapter", out _), "non-success diagnostics remain full by default");
Check(!JsonSerializer.SerializeToElement(stored.Present("operation")).TryGetProperty("adapter", out _), "reading details never mutates the stored default presentation");

var capped = Path.Combine(root, "cap");
using (var cap = new ToolActivityLog(capped, "http-stateless"))
    for (var i = 0; i < 4000; i++) await cap.Invoke(Request(), (_, _) =>
    {
        ToolActivityLog.RecordRuntime(new RuntimeActivity("call", "rtest", "entity", "Component", "Method", "int", [])
            { State = "invoked", Dispatch = "returned", PrepareMs = 3, CompileMs = 2, InvokeMs = 1 });
        return ValueTask.FromResult(new CallToolResult { Content = [new TextContentBlock { Text = "é😀" }] });
    }, default);
var capFile = Directory.GetFiles(capped).Single();
text = ReadLog(capFile);
Check(new FileInfo(capFile).Length <= ToolActivityLog.MaxBytes && text.Contains("calls=4000 completed=4000"), "byte cap with complete summary");
Check(!text.Contains("omitted_detail_lines=0 "), "overflow details counted");
Check(text.Contains("\"Invoked\":4000") && text.Contains("\"CompileMs\":8000"), "runtime totals continue after detail cap");
Check(text.Contains("\"TextBytes\":24000") && text.Contains("\"Responses\":4000"), "response sizes continue after detail cap");
for (var i = 0; i < 7; i++) { using var retained = new ToolActivityLog(capped, "stdio"); }
Check(Directory.GetFiles(capped).Length == 5, "five-session retention");
var blocked = Path.Combine(root, "blocked");
File.WriteAllText(blocked, "not a directory");
using (var broken = new ToolActivityLog(blocked, "stdio"))
    Check(ReferenceEquals(await broken.Invoke(Request(), (_, _) => ValueTask.FromResult(failure), default), failure), "filesystem failure does not break execution");

// Real production process: no Workbench or game configuration, isolated data owner.
var assembly = typeof(ToolActivityLog).Assembly.Location;
Process Start(string config, bool http)
{
    var info = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
    foreach (var arg in new[] { assembly, "--config", config }) info.ArgumentList.Add(arg);
    if (http) info.ArgumentList.Add("--serve-http");
    return Process.Start(info)!;
}
string Rpc(int id, string method, object parameters) => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters });
var stdioData = Path.Combine(root, "stdio");
var stdioConfig = Path.Combine(root, "stdio.json");
File.WriteAllText(stdioConfig, JsonSerializer.Serialize(new { dataDirectory = stdioData }));
using (var process = Start(stdioConfig, false))
{
    var errors = process.StandardError.ReadToEndAsync();
    try
    {
        await process.StandardInput.WriteLineAsync(Rpc(1, "initialize", new { protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "test", version = "1" } }));
        Check((await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20)))!.Contains("result"), "stdio initialization");
        await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
        await process.StandardInput.WriteLineAsync(Rpc(2, "tools/call", new { name = "workbench_project_context", arguments = new { } }));
        Check((await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20)))!.Contains("result"), "stdio production tool response");
        process.StandardInput.Close();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        text = ReadLog(Directory.GetFiles(Path.Combine(stdioData, "tool-activity")).Single());
        Check(text.Contains("state=ended calls=1 completed=1") && text.Contains("tool=workbench_project_context"), "stdio EOF summary and filter wiring");
    }
    finally { if (!process.HasExited) process.Kill(true); await errors; }
}

var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;
listener.Stop();
var httpData = Path.Combine(root, "http");
var tokenFile = Path.Combine(root, "token.key");
var token = new string('a', 40);
File.WriteAllText(tokenFile, token);
var httpConfig = Path.Combine(root, "http.json");
File.WriteAllText(httpConfig, JsonSerializer.Serialize(new { dataDirectory = httpData, localHttp = new { port, tokenFile } }));
using (var process = Start(httpConfig, true))
{
    var errors = process.StandardError.ReadToEndAsync();
    var output = process.StandardOutput.ReadToEndAsync();
    try
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
        for (var i = 0; ; i++)
        {
            try { using var ready = await client.GetAsync("/mcp"); break; }
            catch (HttpRequestException) when (i < 100) { await Task.Delay(100); }
        }
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var response = await client.PostAsync("/mcp", new StringContent(Rpc(1, "tools/call", new { name = "workbench_project_context", arguments = new { } }), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode && (await response.Content.ReadAsStringAsync()).Contains("result");
        }));
        Check(responses.All(x => x), "concurrent stateless HTTP production calls");
        text = ReadLog(Directory.GetFiles(Path.Combine(httpData, "tool-activity")).Single());
        Check(text.Contains("state=active calls=8 completed=8") && text.Contains("scope=host-run"), "HTTP shares host session across requests");
        Check(Enumerable.Range(1, 8).All(i => text.Contains($"START call={i} ")), "HTTP reused request IDs get unique call IDs");
    }
    finally { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); await errors; await output; }
}
Console.WriteLine("All tool activity tests passed.");

