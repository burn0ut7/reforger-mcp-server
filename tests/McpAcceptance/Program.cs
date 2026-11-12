using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ReforgerMcp;

var names = """
capture_windows capture_screenshot capture_sequence
engine_targets engine_prepare engine_job engine_cancel engine_diagnostics engine_search engine_function engine_references
pak_prepare pak_search pak_search_text pak_read pak_extract log_sessions log_read
workbench_status workbench_script_status workbench_project_context workbench_windows workbench_controls
workbench_window_action workbench_window_action_status workbench_open_project workbench_terminate workbench_open_world
workbench_reload_scripts workbench_operation workbench_focus_policy workbench_resource_leak_assertion workbench_script_authorization
workbench_dynamic_choices workbench_load_extension workbench_create_entity workbench_entity workbench_query_entities workbench_inspect_entity workbench_edit_entity
workbench_open_prefab workbench_inspect_prefab workbench_save_prefab_definition workbench_create_child_prefab workbench_save_prefab
workbench_save_world workbench_history workbench_history_status workbench_play workbench_play_status workbench_multiplayer
workbench_multiplayer_status workbench_multiplayer_setting workbench_runtime_setup workbench_runtime_status workbench_runtime_inspect
workbench_runtime_call workbench_runtime_prepare workbench_runtime_invoke
""".Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
if (args is ["--self-test"])
{
    void Check(bool ok) { if (!ok) throw new Exception("Verdict regression"); }
    Check(Verdict.Code(["passed"], true) == 0);
    Check(Verdict.Code(["passed", "not_tested"], true) == 2);
    Check(Verdict.Code(["passed", "blocked"], true) == 2);
    Check(Verdict.Code(["failed", "blocked"], true) == 1);
    Check(Verdict.Code(["passed"], false) == 1);
    Check(Verdict.Code([], true) == 2);
    Check(!Verdict.SemanticOk(JsonSerializer.SerializeToElement(new { state = "failed" })));
    Check(!Verdict.SemanticOk(JsonSerializer.SerializeToElement(new { data = new { ok = false } })));
    Check(!Verdict.SemanticOk(JsonSerializer.SerializeToElement(new { state = "pending" })));
    Check(names.Length == 59 && names.Distinct().Count() == 59);
    Console.WriteLine("10 acceptance-gate regression checks passed."); return 0;
}
if (args.Length != 2) throw new ArgumentException("Usage: McpAcceptance <configuration.json> <new evidence directory> OR --self-test");
var output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Evidence directory must be new.");
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
void Save(string file, object value) => File.WriteAllText(Path.Combine(output, file), JsonSerializer.Serialize(value, json));
var rows = names.ToDictionary(n => n, n => new Row(n, "not_tested", "Requires isolated behavioral fixture; advertisement is not a behavior test."));
var started = DateTimeOffset.UtcNow;
bool contract = false;
string? fatal = null;
var values = new Dictionary<string, JsonElement>();
try
{
    var cfg = Configuration.Load(args[0]);
    var http = cfg.LocalHttp ?? throw new Exception("Shared HTTP server configuration required; no second server is launched.");
    var token = File.ReadAllText(http.TokenFile).Trim();
    using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(12));
    await using var client = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri($"http://127.0.0.1:{http.Port}/mcp"),
        AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + token }
    }), cancellationToken: lifetime.Token);
    var listed = await client.ListToolsAsync(cancellationToken: lifetime.Token);
    var advertised = listed.Select(t => t.Name).ToHashSet();
    var missing = names.Except(advertised).ToArray();
    var extra = advertised.Except(names).ToArray();
    contract = missing.Length == 0 && extra.Length == 0;
    Save("tools.json", listed.Select(t => t.ProtocolTool));
    Save("connection.json", new { client.ServerInfo, endpoint = $"http://127.0.0.1:{http.Port}/mcp", missing, extra,
        note = "Transport and tool names checked. This does not identify the deployed executable or prove schema compatibility." });
    foreach (var name in missing) rows[name] = new(name, "failed", "Required tool missing from tools/list.");
    async Task Probe(string name, object arguments, Func<JsonElement, bool> assertion, string expectation, string? dependency = null, int imageCount = 0)
    {
        if (!advertised.Contains(name)) return;
        if (dependency != null && rows[dependency].Status != "passed")
        { rows[name] = new(name, "blocked", $"Prerequisite {dependency} did not pass."); return; }
        var argsMap = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(arguments))!;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(150));
            var response = await client.CallToolAsync(name, argsMap, cancellationToken: deadline.Token);
            var texts = response.Content.OfType<TextContentBlock>().Select(t => t.Text).ToArray();
            var images = response.Content.OfType<ImageContentBlock>().ToArray();
            Save(name + ".json", new { arguments, response.IsError, texts, imageCount = images.Length });
            if (response.IsError == true) throw new Exception(string.Join("\n", texts));
            var root = JsonDocument.Parse(texts.First()).RootElement.Clone();
            if (!Verdict.SemanticOk(root) || !assertion(root)) throw new Exception("Positive assertion failed: " + expectation);
            if (images.Length != imageCount) throw new Exception($"Expected {imageCount} PNG frames, received {images.Length}.");
            long previousTicks = -1;
            for (var i = 0; i < images.Length; i++)
            {
                var bytes = images[i].DecodedData.ToArray();
                if (images[i].MimeType != "image/png" || bytes.Length < 24 ||
                    !bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
                    throw new Exception("Invalid PNG frame.");
                var metadata = JsonDocument.Parse(texts[i]).RootElement;
                var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
                var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
                var ticks = metadata.GetProperty("frameSystemRelativeTicks").GetInt64();
                if (width <= 0 || height <= 0 || width > 320 || height > 320 ||
                    width != metadata.GetProperty("width").GetInt32() || height != metadata.GetProperty("height").GetInt32() ||
                    ticks <= previousTicks || metadata.GetProperty("windowId").GetString() != argsMap["windowId"]?.ToString())
                    throw new Exception("Frame dimensions, identity or timestamp order invalid.");
                previousTicks = ticks;
                File.WriteAllBytes(Path.Combine(output, $"{name}-{i}.png"), bytes);
            }
            values[name] = root;
            rows[name] = new(name, "passed", expectation + " (bounded smoke coverage only)");
        }
        catch (Exception ex) { rows[name] = new(name, "failed", ex.Message.Replace(token, "[redacted]")); }
        Console.WriteLine($"{name}: {rows[name].Status}");
        Save("progress.json", rows.Values);
    }
    bool ArrayHas(JsonElement e, string property) => e.TryGetProperty(property, out var a) && a.ValueKind == JsonValueKind.Array && a.GetArrayLength() > 0;
    await Probe("workbench_status", new { }, e => e.GetProperty("workbench").GetProperty("state").GetString() == "running", "Workbench running");
    await Probe("engine_targets", new { }, e => ArrayHas(e, "targets"), "Configured binary identities returned");
    await Probe("pak_search", new { kind = "script", source = "game", limit = 1 }, e => ArrayHas(e, "items") && e.GetProperty("revision").GetString()?.Length > 0, "Script found with catalogue revision");
    if (values.TryGetValue("pak_search", out var pak))
    {
        var id = pak.GetProperty("items")[0].GetProperty("id").GetString();
        var revision = pak.GetProperty("revision").GetString();
        await Probe("pak_read", new { entryId = id, revision, limit = 5 }, e => ArrayHas(e, "lines"), "Nonempty source lines returned for selected entry", "pak_search");
        await Probe("pak_extract", new { entryId = id, revision }, e => {
            var path = e.GetProperty("file").GetString()!;
            return File.Exists(path) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(e.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase);
        }, "Extracted bytes match reported SHA256", "pak_search");
        await Probe("pak_search_text", new { query = "class", source = "game", limit = 1, revision }, e => ArrayHas(e, "files"), "Source text match returned", "pak_search");
    }
    await Probe("log_sessions", new { source = "workbench", limit = 1 }, e => ArrayHas(e, "sessions"), "Workbench log session exists");
    await Probe("log_read", new { source = "workbench", scope = "tail", limit = 5 }, e => ArrayHas(e, "entries") && e.GetProperty("revision").GetString()?.Length > 0, "Nonempty log tail with revision; not a compilation-success assertion", "log_sessions");
    await Probe("capture_windows", new { }, e => ArrayHas(e, "windows"), "Configured visible capture windows returned");
    if (values.TryGetValue("capture_windows", out var captures))
    {
        var window = captures.GetProperty("windows").EnumerateArray().FirstOrDefault(w => !w.GetProperty("minimized").GetBoolean());
        if (window.ValueKind != JsonValueKind.Undefined)
        {
            var id = window.GetProperty("windowId").GetString();
            await Probe("capture_screenshot", new { windowId = id, maxDimension = 320 }, e => e.ValueKind == JsonValueKind.Object, "One PNG captured", "capture_windows", 1);
            await Probe("capture_sequence", new { windowId = id, maxDimension = 320, frameCount = 2 }, e => e.ValueKind == JsonValueKind.Object, "Two PNG frames captured", "capture_windows", 2);
        }
    }
    await Probe("workbench_windows", new { details = false }, e => ArrayHas(e, "windows"), "Native window headers returned");
    if (values.TryGetValue("workbench_windows", out var windows))
    {
        var pid = windows.GetProperty("processId").GetInt32();
        await Probe("workbench_windows", new { details = true }, e => ArrayHas(e, "windows"), "Detailed window inspection returned");
        await Probe("workbench_controls", new { selectionId = values["workbench_windows"].GetProperty("windows")[0].GetProperty("windowId").GetString() },
            e => e.ValueKind == JsonValueKind.Object, "Control inspection returned", "workbench_windows");
        await Probe("workbench_project_context", new { processId = pid, limit = 1 }, e => e.GetProperty("state").GetString() == "observed" && e.GetProperty("projectToken").GetString()?.Length > 0, "Live project identity returned");
        foreach (var name in new[] {"workbench_focus_policy", "workbench_script_authorization", "workbench_resource_leak_assertion"})
            await Probe(name, new { action = "status", processId = pid }, e => name == "workbench_focus_policy"
                    ? e.GetProperty("state").GetString() is "enabled" or "disabled"
                    : e.GetProperty("state").GetString() == "observed" && e.GetProperty("allocationsReleased").GetBoolean()
                        && e.GetProperty("actualThread").GetUInt32() == e.GetProperty("expectedThread").GetUInt32(),
                "Native policy status and applicable callback completion verified");
        if (rows["workbench_project_context"].Status != "passed")
            foreach (var name in names.Where(n => n.StartsWith("workbench_") && rows[n].Status == "not_tested"))
                rows[name] = new(name, "blocked", "Live project compatibility prerequisite failed; no editor mutations attempted.");
    }
    if (values.TryGetValue("engine_targets", out var targets) && targets.GetProperty("targets").EnumerateArray().Any(t => t.GetProperty("identity").GetProperty("target").GetString() == "workbench" && t.GetProperty("state").GetString() == "ready"))
    {
        await Probe("engine_diagnostics", new { target = "workbench", limit = 1 }, e => e.GetProperty("total").GetInt32() >= 0 && e.TryGetProperty("identity", out _), "Prepared analysis diagnostics readable");
        await Probe("engine_search", new { target = "workbench", query = "Workbench", kind = "strings", limit = 1 }, e => e.GetProperty("data").GetProperty("ok").GetBoolean() && ArrayHas(e.GetProperty("data"), "items"), "Workbench string found in prepared analysis");
        if (values.TryGetValue("engine_search", out var search))
        {
            var item = search.GetProperty("data").GetProperty("items")[0];
            var key = search.GetProperty("identity").GetProperty("analysisKey").GetString();
            await Probe("engine_references", new { target = "workbench", address = item.GetProperty("address").GetString(), analysisKey = key, limit = 1 },
                e => e.GetProperty("data").GetProperty("ok").GetBoolean(), "Reference query succeeds at discovered string with pinned analysis");
            if (item.TryGetProperty("references", out var refs) && refs.GetArrayLength() > 0 && refs[0].TryGetProperty("functionAddress", out var address))
                await Probe("engine_function", new { target = "workbench", address = address.GetString(), analysisKey = key, limit = 1 },
                    e => e.GetProperty("data").GetProperty("ok").GetBoolean(), "Function inspection succeeds at discovered reference with pinned analysis");
        }
    }
}
catch (Exception ex) { fatal = ex.GetType().Name + ": " + ex.Message; }
var code = fatal != null ? 1 : Verdict.Code(rows.Values.Select(r => r.Status), contract);
Save("report.json", new { started, finished = DateTimeOffset.UtcNow, updateSucceeded = code == 0,
    exitCode = code, contractNamesMatch = contract, fatal,
    scope = "Safe live smoke checks. Unimplemented fixture/lifecycle tests remain not_tested; these prevent update approval. No compile, Play, project switch, save, rebuild or server replacement is performed.",
    counts = rows.Values.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count()), tools = rows.Values });
Console.WriteLine($"Update acceptance: {(code == 0 ? "PASSED" : "NOT CONFIRMED")}; report: {Path.Combine(output, "report.json")}");
return code;

record Row(string Tool, string Status, string Evidence);
static class Verdict
{
    public static int Code(IEnumerable<string> statuses, bool contract)
    {
        var rows = statuses.ToArray();
        return !contract || rows.Contains("failed") ? 1 : rows.Length > 0 && rows.All(s => s == "passed") ? 0 : 2;
    }
    public static bool SemanticOk(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return false;
        if (e.TryGetProperty("isError", out var error) && error.ValueKind == JsonValueKind.True) return false;
        if (e.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False) return false;
        if (e.TryGetProperty("state", out var state) && new[] {"failed","blocked","pending","unsupported","unavailable","not_configured","outcome_unknown","suppression_failed"}.Contains(state.GetString())) return false;
        return !e.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object || SemanticOk(data);
    }
}


