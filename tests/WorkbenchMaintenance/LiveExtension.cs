using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ReforgerMcp;

internal static class LiveExtension
{
    internal static async Task<int> Run(string[] args)
    {
        if (args.Length != 5)
            throw new ArgumentException("--extension-live <configuration.json> <Workbench PID> <diagnostic DLL> <new evidence directory>");
        var pid = int.Parse(args[2]);
        var choices = args[0] == "--extension-choices-live";
        var dll = Path.GetFullPath(args[3]);
        var output = Path.GetFullPath(args[4]);
        if (Path.Exists(output)) throw new IOException("Use a new evidence directory.");
        if (!File.Exists(dll)) throw new FileNotFoundException("Diagnostic DLL missing", dll);
        Directory.CreateDirectory(output);
        var reportPath = Path.Combine(output, "extension.jsonl");
        var expectedHash = NativeHookUpdate.HashFile(dll);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var checks = new List<object>();
        string token = "";
        string? failure = null;
        int sequence = 0;
        bool passed = false;
        void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json));
        void Check(bool condition, string name)
        {
            checks.Add(new { name, passed = condition });
            if (!condition) throw new InvalidOperationException(name);
            Console.WriteLine("PASS " + name);
        }
        try
        {
            var configuration = Configuration.Load(args[1]);
            var http = configuration.LocalHttp ?? throw new InvalidOperationException("Use the existing shared HTTP server.");
            token = File.ReadAllText(http.TokenFile).Trim();
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri($"http://127.0.0.1:{http.Port}/mcp"),
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + token }
            }), cancellationToken: deadline.Token);
            var tools = await client.ListToolsAsync(cancellationToken: deadline.Token);
            var loader = tools.SingleOrDefault(t => t.Name == "workbench_load_extension");
            Check(loader is not null, "Current MCP catalogue exposes exact-DLL loading");
            Save("loader-schema.json", loader!.ProtocolTool);
            async Task<JsonElement> Call(string tool, object arguments)
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!;
                while (true)
                {
                    var response = await client.CallToolAsync(tool, map, cancellationToken: deadline.Token);
                    var text = string.Join("\n", response.Content.OfType<TextContentBlock>().Select(b => b.Text));
                    Save($"{++sequence:000}-{tool}.json", new { arguments = map, response.IsError, text });
                    if (response.IsError == true) throw new InvalidOperationException(tool + ": " + text);
                    var value = JsonDocument.Parse(text).RootElement.Clone();
                    if (!value.TryGetProperty("poll", out var poll) ||
                        value.TryGetProperty("operationCompleted", out var complete) && complete.GetBoolean()) return value;
                    tool = poll.GetProperty("tool").GetString()!;
                    if (tool != "workbench_operation") throw new InvalidOperationException("Unexpected poll owner");
                    map = JsonSerializer.Deserialize<Dictionary<string, object?>>(poll.GetProperty("arguments").GetRawText())!;
                    await Task.Delay(250, deadline.Token);
                }
            }
            var before = await Call("workbench_project_context", new { processId = pid, scope = "all", limit = 100 });
            Check(before.GetProperty("state").GetString() == "observed", "Initial project observed");
            Check(before.GetProperty("addons").GetArrayLength() == before.GetProperty("total").GetInt32(), "Complete addon baseline fits observation");
            var specification = reportPath;
            if (choices)
            {
                var adapter = await Call("workbench_runtime_setup", new { action = "status", processId = pid });
                Check(adapter.GetProperty("state").GetString() == "ready", "Existing script adapter is ready");
                Save("choices-config.json", new { schema = 1, mode = "choices", reportPath,
                    port = adapter.GetProperty("port").GetInt32(), adapterBuild = adapter.GetProperty("adapterBuild").GetString() });
                specification = "@" + Path.Combine(output, "choices-config.json");
            }
            // One mutation only. On timeout/failure, inspect this receipt and process;
            // never reload the DLL to get a different test result.
            var loaded = await Call("workbench_load_extension", new { processId = pid, filePath = dll,
                expectedSha256 = expectedHash, specification });
            var native = loaded.GetProperty("result");
            Check(loaded.GetProperty("selectedModuleObserved").GetBoolean() && native.GetProperty("moduleLoaded").GetBoolean(), "Selected DLL observed resident");
            Check(native.GetProperty("state").GetString() == "entry_returned" && native.GetProperty("returnCodeOrWin32Error").GetInt32() == 1,
                "Extension initializer reports started");
            Check(native.GetProperty("expectedThread").GetUInt32() == native.GetProperty("actualThread").GetUInt32() &&
                native.GetProperty("allocationsReleased").GetBoolean(), "Owning editor thread and temporary callback cleanup");
            Check(File.Exists(reportPath), "DLL produced its independent diagnostic report");
            // Qt keeps the diagnostic stream open for later drag observations.
            // Our observer must share that writer; no DLL reload is needed to read it.
            using var reportStream = new FileStream(reportPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reportReader = new StreamReader(reportStream);
            var startup = JsonDocument.Parse(reportReader.ReadLine() ?? throw new IOException("Missing startup record")).RootElement.Clone();
            Check(startup.GetProperty("event").GetString() == "startup" && startup.GetProperty("state").GetString() == (choices ? "choices_initializing" : "diagnostics_only") &&
                !startup.GetProperty("authoringEnabled").GetBoolean() && startup.GetProperty("pid").GetInt32() == pid,
                "Independent report confirms correct host and requested startup mode");
            if (choices)
            {
                var hook = JsonDocument.Parse(reportReader.ReadLine() ?? throw new IOException("Missing hook result")).RootElement.Clone();
                Check(hook.GetProperty("event").GetString() == "propertyHook" && hook.GetProperty("state").GetString() == "installed" &&
                    hook.GetProperty("authoringEnabled").GetBoolean(), "Owning property hook reports installed");
            }
            var identity = startup.GetProperty("extension");
            Check(Path.GetFullPath(identity.GetProperty("path").GetString()!).Equals(dll, StringComparison.OrdinalIgnoreCase) &&
                identity.GetProperty("sha256").GetString()!.Equals(expectedHash, StringComparison.OrdinalIgnoreCase),
                "DLL self-reported identity matches selected build");
            var after = await Call("workbench_project_context", new { processId = pid, scope = "all", limit = 100 });
            Check(after.GetProperty("addons").GetArrayLength() == after.GetProperty("total").GetInt32(), "Complete final addon observation");
            string ProjectIdentity(JsonElement context) => JsonSerializer.Serialize(new
            {
                current = context.GetProperty("currentProject").GetString(),
                addons = context.GetProperty("addons").EnumerateArray().Select(a => new
                {
                    guid = a.GetProperty("guid").GetString(), path = a.GetProperty("projectFile").GetString(),
                    loaded = a.GetProperty("loaded").GetBoolean(), current = a.GetProperty("current").GetBoolean()
                })
            });
            Check(after.GetProperty("state").GetString() == "observed" && ProjectIdentity(before) == ProjectIdentity(after),
                "Current project and addon identities preserved");
            Check(NativeHookUpdate.HashFile(dll) == expectedHash, "DLL file unchanged after loading");
            passed = true;
        }
        catch (Exception ex) { failure = token.Length == 0 ? ex.Message : ex.Message.Replace(token, "[redacted]"); }
        Save("report.json", new { passed, failure, checks, pid, dll, expectedHash,
            scope = choices ? "Live choice-hook installation only; not dropdown or commit validation. DLL remains resident until Workbench exits."
                : "Live diagnostic DLL initialization only. DLL intentionally remains resident until Workbench exits.",
            unverified = new[] { "drag observation", "shutdown", "startup without MCP", "dropdowns", "drop conversion", "undo/save", "vanilla-game compatibility" } });
        Console.WriteLine(passed ? "Live extension initialization PASSED" : "Live extension initialization FAILED: " + failure);
        return passed ? 0 : 1;
    }
}
