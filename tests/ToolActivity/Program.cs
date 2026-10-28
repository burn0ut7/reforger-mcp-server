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

var capped = Path.Combine(root, "cap");
using (var cap = new ToolActivityLog(capped, "http-stateless"))
    for (var i = 0; i < 4000; i++) await cap.Invoke(Request(), (_, _) => ValueTask.FromResult(new CallToolResult()), default);
var capFile = Directory.GetFiles(capped).Single();
text = ReadLog(capFile);
Check(new FileInfo(capFile).Length <= ToolActivityLog.MaxBytes && text.Contains("calls=4000 completed=4000"), "byte cap with complete summary");
Check(!text.Contains("omitted_detail_lines=0 "), "overflow details counted");
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

