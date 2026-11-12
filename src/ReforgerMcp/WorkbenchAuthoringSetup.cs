using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ReforgerMcp;

// A convenience client for the existing owners; no second installation or loader path.
internal static class WorkbenchAuthoringSetup
{
    internal static async Task<bool> Run(Configuration configuration, string configurationPath, string dllPath, CancellationToken ct, bool automatic = false)
    {
        var dll = Path.GetFullPath(dllPath);
        if (!File.Exists(dll) || !Path.GetExtension(dll).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose an existing authoring DLL.");
        using var process = WorkbenchSession.SelectProcess(configuration);
        var control = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GCWorkbench");
        var startedTicks = process.StartTime.ToUniversalTime().Ticks;
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll)));
        var resident = process.Modules.Cast<System.Diagnostics.ProcessModule>().FirstOrDefault(module =>
            Path.GetFileName(module.FileName).Equals(Path.GetFileName(dll), StringComparison.OrdinalIgnoreCase));
        if (resident is not null)
        {
            if (automatic && Path.GetFullPath(resident.FileName).Equals(dll, StringComparison.OrdinalIgnoreCase)
                && WorkbenchAuthoringStartup.HasVerifiedLoad(control, process.Id, startedTicks, dll, hash)) return true;
            throw new InvalidOperationException("An authoring DLL is already resident, but this startup did not verify the selected installation. Save and restart Workbench before loading or changing builds.");
        }
        // Standalone packages use a short-lived instance of the same tool server.
        // Configured shared-server clients retain their existing transport.
        IClientTransport transport;
        if (configuration.LocalHttp is { } http)
            transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri($"http://127.0.0.1:{http.Port}/mcp"),
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + File.ReadAllText(http.TokenFile).Trim() }
            });
        else
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the setup helper.");
            var arguments = new List<string>();
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                arguments.Add(typeof(WorkbenchAuthoringSetup).Assembly.Location);
            arguments.AddRange(["--config", Path.GetFullPath(configurationPath)]);
            transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = executable,
                Arguments = arguments,
                Name = "GC Workbench setup"
            });
        }
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        async Task<JsonElement> Call(string tool, object arguments)
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!;
            while (true)
            {
                var response = await client.CallToolAsync(tool, map, cancellationToken: ct);
                var text = string.Join("\n", response.Content.OfType<TextContentBlock>().Select(block => block.Text));
                if (response.IsError == true) throw new InvalidOperationException(text);
                var value = JsonDocument.Parse(text).RootElement.Clone();
                if (value.TryGetProperty("dispatched", out var dispatched) && !dispatched.GetBoolean())
                    throw new InvalidOperationException("Another Workbench operation is pending; this setup step was not dispatched. Inspect that operation before starting setup again.");
                if (!value.TryGetProperty("poll", out var poll) ||
                    value.TryGetProperty("operationCompleted", out var completed) && completed.GetBoolean()) return value;
                tool = poll.GetProperty("tool").GetString()!;
                if (tool != "workbench_operation") throw new InvalidOperationException("Unexpected operation owner; inspect the existing operation before retrying.");
                map = JsonSerializer.Deserialize<Dictionary<string, object?>>(poll.GetProperty("arguments").GetRawText())!;
                Console.Error.WriteLine($"Waiting for Workbench operation {map["operationId"]}");
                await Task.Delay(500, ct);
            }
        }

        JsonElement adapter;
        if (automatic)
        {
            // Startup is read-only until the installed adapter is ready. Never reload
            // scripts, change preferences, or interrupt Play in the background.
            try { adapter = await Call("workbench_runtime_setup", new { action = "status", processId = process.Id }); }
            catch (InvalidOperationException ex)
            {
                await File.WriteAllTextAsync(Path.Combine(configuration.DataDirectory, "waiting.txt"), ex.Message, ct);
                return false;
            }
            if (adapter.GetProperty("state").GetString() != "ready")
            {
                await File.WriteAllTextAsync(Path.Combine(configuration.DataDirectory, "waiting.txt"), adapter.GetRawText(), ct);
                return false;
            }
            if (adapter.GetProperty("adapter").GetProperty("state").GetString() != "not_playing") return false;
            // Survives a watcher restart: an uncertain native load is never replayed.
            WorkbenchAuthoringStartup.ClaimLoadAttempt(control, process.Id, startedTicks);
        }
        else adapter = await Call("workbench_runtime_setup", new { action = "install", processId = process.Id });
        if (adapter.GetProperty("state").GetString() != "ready")
            throw new InvalidOperationException("Workbench adapter is not ready: " + adapter.GetRawText());
        var folder = Path.Combine(configuration.DataDirectory, "authoring-sessions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var report = Path.Combine(folder, "extension.jsonl");
        var settings = Path.Combine(folder, "choices.json");
        await File.WriteAllTextAsync(settings, JsonSerializer.Serialize(new
        {
            schema = 1,
            mode = "choices",
            reportPath = report,
            port = adapter.GetProperty("port").GetInt32(),
            adapterBuild = adapter.GetProperty("adapterBuild").GetString()
        }, Configuration.Json), ct);
        try
        {
            var loaded = await Call("workbench_load_extension", new
            {
                processId = process.Id,
                filePath = dll,
                expectedSha256 = hash,
                specification = "@" + settings
            });
            await File.WriteAllTextAsync(Path.Combine(folder, "load.json"), loaded.GetRawText(), ct);
            if (!loaded.GetProperty("selectedModuleObserved").GetBoolean() ||
                loaded.GetProperty("result").GetProperty("returnCodeOrWin32Error").GetInt32() != 1)
                throw new InvalidOperationException("Authoring initialization did not succeed. Inspect " + folder + "; do not reload the DLL.");
            using var stream = new FileStream(report, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            _ = await reader.ReadLineAsync(ct);
            using var hook = JsonDocument.Parse(await reader.ReadLineAsync(ct) ?? "{}");
            if (!hook.RootElement.TryGetProperty("state", out var state) || state.GetString() != "installed" ||
                !hook.RootElement.TryGetProperty("authoringEnabled", out var enabled) || !enabled.GetBoolean())
                throw new InvalidOperationException("String picker hook was not installed. Inspect " + folder + "; do not reload the DLL.");
            Console.WriteLine("String choices enabled for this Workbench session. Reopen the selected component's properties to show its pickers.");
            Console.WriteLine("Session details: " + folder);
            WorkbenchAuthoringStartup.RecordVerifiedLoad(control, process.Id, startedTicks, dll, hash);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Authoring setup did not confirm completion. Inspect " + folder + "; do not repeat a potentially dispatched load. " + ex.Message, ex);
        }
    }
}
