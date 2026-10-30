using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using ReforgerMcp;

var root = Path.GetFullPath(Path.Combine(".temp", "pak-search-tests", Guid.NewGuid().ToString("N")));
var packed = Path.Combine(root, "archives");
var loose = Path.Combine(root, "loose");
Directory.CreateDirectory(packed);
Directory.CreateDirectory(loose);
var ladder = "Entity {\r\n components {\r\n  LadderComponent {\r\n   LadderStepLength 0.33\r\n  }\r\n }\r\n}\r\n";
var files = new Dictionary<string, byte[]>
{
    ["ladder.et"] = Encoding.UTF8.GetBytes(ladder),
    ["ladder-copy.et"] = Encoding.UTF8.GetBytes(ladder),
    ["screen.layout"] = Encoding.UTF8.GetBytes("FrameWidgetClass {\n Text \"échelle\"\n}\n" + new string('x', 2100) + "long-marker\n" + string.Concat(Enumerable.Repeat(new string('界', 700) + "\n", 50))),
    ["settings.conf"] = Encoding.UTF8.GetBytes("Config { LadderStepLength 0.33 }\n"),
    ["world.ent"] = Encoding.UTF8.GetBytes("Entity { LadderComponent {} }\n"),
    ["terrain.layer"] = Encoding.UTF8.GetBytes("Entity { LadderComponent {} }\n"),
    ["logic.c"] = Encoding.UTF8.GetBytes("class LadderComponent {}\n"),
    ["broken.et"] = [0xff, 0xfe, 0],
    ["binary.layout"] = [0, 1, 2],
    ["image.edds"] = Encoding.UTF8.GetBytes("LadderComponent")
};
WritePak(Path.Combine(packed, "sample.pak"), files);
File.WriteAllText(Path.Combine(loose, "local.et"), ladder);
var config = new Configuration { DataDirectory = Path.Combine(root, "store"),
    PakDirectories = new() { ["packed"] = packed }, UnpackedDirectories = new() { ["local"] = loose } };
var store = new PakStore(config);
JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, Configuration.Json);
JsonElement[] Hits(JsonElement result) => result.GetProperty("files").EnumerateArray().SelectMany(file =>
    file.GetProperty("matches").EnumerateArray().Select(hit => Json(new
    {
        entryId = file.GetProperty("entryId").GetString(), line = hit.GetProperty("line").GetInt32(),
        text = hit.GetProperty("text").GetString(), truncated = hit.GetProperty("truncated").GetBoolean(),
        sha256 = file.GetProperty("sha256").GetString()
    }))).ToArray();
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
async Task Reject(Func<Task<object>> action, string name)
{
    try { await action(); }
    catch (Exception e) when (e is InvalidOperationException or InvalidDataException or ArgumentException or OperationCanceledException or ModelContextProtocol.McpException)
    { Console.WriteLine("PASS " + name); return; }
    throw new Exception("Expected rejection: " + name);
}
using var workbench = new Workbench(config);
var readiness = new WorkbenchTools(workbench, store);
var combined = Json(await readiness.Status());
Check(combined.GetProperty("workbench").GetProperty("state").GetString() == "not_configured" &&
    combined.GetProperty("pak").GetProperty("state").GetString() == "unavailable",
    "combined status reports unconfigured Workbench and unprepared catalogue independently");
Check(!Directory.Exists(Path.Combine(config.DataDirectory, "paks")), "status does not prepare catalogue");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    try { await readiness.Status(cancelled.Token); throw new Exception("Status ignored cancellation"); }
    catch (OperationCanceledException) { Console.WriteLine("PASS combined status preserves cancellation"); }
}
var status = Json(await store.Prepare(default));
var revision = status.GetProperty("revision").GetString()!;
combined = Json(await readiness.Status(details: true));
Check(JsonElement.DeepEquals(combined.GetProperty("pak"), Json(await store.Status(default))),
    "combined status preserves full catalogue readiness and provenance");
var compactReadiness = Json(await readiness.Status());
Check(compactReadiness.GetProperty("pak").GetProperty("revision").GetString() == revision &&
    compactReadiness.GetProperty("pak").GetProperty("sources").GetArrayLength() == 2 &&
    !compactReadiness.GetProperty("pak").TryGetProperty("excludedExamples", out _) &&
    compactReadiness.GetRawText().Length < combined.GetRawText().Length / 2,
    "compact readiness retains revision/source identity with less than half the text");
File.WriteAllText(Path.Combine(loose, "status-stale.et"), ladder);
combined = Json(await readiness.Status());
Check(combined.GetProperty("pak").GetProperty("state").GetString() == "unavailable" &&
    combined.GetProperty("pak").GetProperty("reason").GetString()!.Contains("stale") &&
    combined.GetProperty("workbench").GetProperty("state").GetString() == "not_configured",
    "stale catalogue retains independent Workbench observation");
File.Delete(Path.Combine(loose, "status-stale.et"));
Check(status.GetProperty("searchableFiles").GetInt32() == 8, "packed and loose resource/script coverage");
Check(status.GetProperty("excludedTextFiles").GetInt32() == 2, "binary/invalid UTF-8 exclusions explicit");
var manifestPath = status.GetProperty("manifest").GetString()!;
var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
var duplicates = manifest["textFiles"]!.AsArray().Where(x => x!["entryId"]!.GetValue<string>().EndsWith("ladder.et") ||
    x!["entryId"]!.GetValue<string>().EndsWith("ladder-copy.et")).ToArray();
Check(duplicates.Length == 2 && duplicates[0]!["offset"]!.GetValue<long>() == duplicates[1]!["offset"]!.GetValue<long>(), "identical packed content shares storage without merging identities");
var result = Json(await store.SearchText("laddercomponent", "", 0, 2, revision, default));
Check(result.GetProperty("total").GetInt32() == 6, "case-insensitive matches include scripts, prefabs, worlds and layers");
Check(result.GetProperty("excludedUnreadableFiles").GetInt32() == 2, "search reports exclusions");
Check(result.GetProperty("nextOffset").GetInt32() == 2, "bounded continuation");
var next = Json(await store.SearchText("laddercomponent", "", 2, 10, revision, default));
Check(Hits(next).Length == 4, "continuation covers remaining entries including duplicate provenance");
var prefabs = Json(await store.SearchText("LadderComponent", ".et", 0, 10, revision, default, "packed"));
Check(prefabs.GetProperty("total").GetInt32() == 2, "existing source/path filters narrow resource search");
var hit = Hits(prefabs)[0];
Check(hit.GetProperty("line").GetInt32() == 3, "CRLF line numbers preserved");
var read = Json(await store.ReadText(hit.GetProperty("entryId").GetString()!, 2, 2, revision, default));
Check(read.GetProperty("lines")[0].GetProperty("text").GetString() == hit.GetProperty("text").GetString(), "search hits read back exactly");
Check(read.GetProperty("sha256").GetString() == hit.GetProperty("sha256").GetString(), "search/read byte provenance agrees");
var readRequests = new[] { new PakReadRequest(hit.GetProperty("entryId").GetString()!, 2, 2),
    new PakReadRequest(hit.GetProperty("entryId").GetString()!, 0, 1),
    new PakReadRequest("packed/sample.pak/broken.et"), new PakReadRequest("missing") };
var reads = Json(await store.ReadBatch(readRequests, revision, default));
var firstRead = reads.GetProperty("results")[0];
Check(reads.GetProperty("results").GetArrayLength() == 4 && JsonElement.DeepEquals(firstRead.GetProperty("lines"), read.GetProperty("lines")) &&
    firstRead.GetProperty("sha256").GetString() == read.GetProperty("sha256").GetString(), "batch windows match single read content and provenance");
Check(reads.GetProperty("results")[1].GetProperty("offset").GetInt32() == 0 &&
    reads.GetProperty("results")[2].TryGetProperty("error", out _) && reads.GetProperty("results")[3].GetProperty("entryId").GetString() == "missing",
    "batch preserves repeated-file request order and isolated decode/identity errors");
var budgetReads = Enumerable.Range(0, 8).Select(_ => new PakReadRequest("packed/sample.pak/screen.layout", 4, 20)).ToArray();
var bounded = Json(await store.ReadBatch(budgetReads, revision, default));
Check(bounded.GetProperty("limited").GetBoolean() && JsonSerializer.SerializeToUtf8Bytes(bounded, Configuration.Json).Length <= 32 * 1024,
    "aggregate byte budget includes escaped Unicode and metadata");
foreach (var item in bounded.GetProperty("results").EnumerateArray())
    Check(item.GetProperty("nextOffset").GetInt32() == item.GetProperty("offset").GetInt32() + item.GetProperty("lines").GetArrayLength(),
        "budget continuation covers omitted lines");
var continued = bounded.GetProperty("results")[0];
var resumed = Json(await store.ReadText(continued.GetProperty("entryId").GetString()!, continued.GetProperty("nextOffset").GetInt32(), 2, revision, default));
Check(resumed.GetProperty("lines")[0].GetProperty("line").GetInt32() == continued.GetProperty("nextOffset").GetInt32() + 1,
    "limited batch continues through ordinary single read");
await Reject(() => store.ReadBatch([], revision, default), "empty read batch rejected");
await Reject(() => store.ReadBatch(Enumerable.Repeat(readRequests[0], 9).ToArray(), revision, default), "oversized read batch rejected");
await Reject(() => store.ReadBatch([new("missing", 0, 100), new("missing", 0, 100), new("missing", 0, 1)], revision, default), "aggregate line limit enforced");
await Reject(() => store.ReadBatch(readRequests, "wrong", default), "wrong batch revision fails whole request");
await Reject(() => store.ReadBatch([readRequests[0]], null, default), "batch paging requires revision");
var pakTools = new PakTools(store);
try
{
    await pakTools.SearchText(queries: ["LadderComponent"], limit: 8);
    throw new Exception("Batch accepted an incompatible top-level limit");
}
catch (ModelContextProtocol.McpException ex)
{
    Check(ex.Message.Contains("omit limit and offset") && ex.Message.Contains("Use query"),
        "batch validation explains exactly how to repair the request");
}
var defaultWindow = Json(await pakTools.Read(entryId: "packed/sample.pak/screen.layout", revision: revision));
var largerWindow = Json(await pakTools.Read(entryId: "packed/sample.pak/screen.layout", limit: 30, revision: revision));
Check(defaultWindow.GetProperty("lines").GetArrayLength() == 20 && largerWindow.GetProperty("lines").GetArrayLength() == 30 &&
    defaultWindow.GetProperty("nextOffset").GetInt32() == 20,
    "tool defaults to 20 lines with explicit larger windows and continuation");
await Reject(() => pakTools.Read(entryId: "missing", reads: readRequests), "single and batch inputs mutually exclusive");
await Reject(() => pakTools.Read(reads: readRequests, limit: 10), "batch limits belong to each request");
var batch = Json(await store.SearchBatch(["LadderComponent", "LadderStepLength 0.33", "échelle"], revision: revision));
Check(Hits(batch.GetProperty("groups")[0]).Length == 3, "batch previews bounded");
Check(batch.GetProperty("groups")[1].GetProperty("total").GetInt32() == 4, "attribute/value all-term line search");
Check(batch.GetProperty("groups")[2].GetProperty("total").GetInt32() == 1, "Unicode layout text searchable");
var longSingle = Json(await store.SearchText("long-marker", ".layout", 0, 10, revision, default));
var longBatch = Json(await store.SearchBatch(["long-marker"], pathQuery: ".layout", revision: revision));
Check(Hits(longSingle)[0].GetProperty("truncated").GetBoolean() && Hits(longSingle)[0].GetProperty("text").GetString()!.Length == 2000 &&
    Hits(longBatch.GetProperty("groups")[0])[0].GetProperty("truncated").GetBoolean() &&
    Hits(longBatch.GetProperty("groups")[0])[0].GetProperty("text").GetString()!.Length == 180,
    "grouped matches preserve single/batch truncation bounds");
await Reject(() => store.SearchText("x", "", 1, 10, null, default), "paging requires revision");
await Reject(() => store.SearchText("x", "", 0, 10, "wrong", default), "wrong revision rejected");
using (var cancel = new CancellationTokenSource())
{
    cancel.Cancel();
    await Reject(() => store.SearchText("x", "", 0, 10, revision, cancel.Token), "cancellation preserved");
}
store = new PakStore(config);
Check(Json(await store.SearchText("échelle", "", 0, 10, revision, default)).GetProperty("total").GetInt32() == 1, "restart reuses text store");
var local = Path.Combine(loose, "local.et");
var timestamp = File.GetLastWriteTimeUtc(local);
File.WriteAllText(local, ladder.Replace("0.33", "0.44"));
File.SetLastWriteTimeUtc(local, timestamp);
await Reject(() => store.SearchText("x", "", 0, 10, revision, default), "same-size same-time loose resource edit rejected");
status = Json(await store.Prepare(default));
revision = status.GetProperty("revision").GetString()!;
Check(Json(await store.SearchText("0.44", "", 0, 10, revision, default)).GetProperty("total").GetInt32() == 1, "refresh observes resource edit");
var contentPath = status.GetProperty("textStore").GetString()!;
using (var file = new FileStream(contentPath, FileMode.Open, FileAccess.Write)) file.WriteByte(42);
await Reject(() => store.SearchText("x", "", 0, 10, revision, default), "corrupt content rejected without source fallback");
using (var oversized = File.Create(Path.Combine(loose, "oversized.et"))) oversized.SetLength(PakArchive.MaxPayload + 1L);
status = Json(await store.Prepare(default));
Check(status.GetProperty("excludedTextFiles").GetInt32() == 3, "oversized resource excluded explicitly without blocking other examples");
manifestPath = status.GetProperty("manifest").GetString()!;
manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
manifest["format"] = 3;
manifest.AsObject().Remove("textFiles");
File.WriteAllText(manifestPath, manifest.ToJsonString());
var pointerPath = Path.Combine(config.DataDirectory, "paks", "current.json");
var pointer = JsonNode.Parse(File.ReadAllText(pointerPath))!;
pointer["manifestSha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifestPath))).ToLowerInvariant();
File.WriteAllText(pointerPath, pointer.ToJsonString());
store = new PakStore(config);
Check(Json(await store.Status(default)).GetProperty("state").GetString() == "unavailable", "old script-only catalogue requires explicit preparation");
await Reject(() => store.SearchText("x", "", 0, 10, null, default), "old catalogue cannot silently claim resource coverage");

// Cancel after the production scanner opens its corpus, rather than before dispatch.
var parallelRoot = Path.Combine(root, "parallel");
var parallelInput = Path.Combine(parallelRoot, "input");
Directory.CreateDirectory(parallelInput);
var repeated = string.Concat(Enumerable.Repeat("LadderComponent { LadderStepLength 0.33 }\n", 110_000));
WritePak(Path.Combine(parallelInput, "parallel.pak"), Enumerable.Range(0, 12).ToDictionary(
    i => $"resource{i:D2}.et", i => Encoding.UTF8.GetBytes($"// Resource {i}\n" + repeated)));
var parallelStore = new PakStore(new Configuration { DataDirectory = Path.Combine(parallelRoot, "store"),
    PakDirectories = new() { ["parallel"] = parallelInput } });
var parallelStatus = Json(await parallelStore.Prepare(default));
var parallelContent = parallelStatus.GetProperty("textStore").GetString()!;
using (var activeCancellation = new CancellationTokenSource())
{
    bool scanning = false;
    parallelStore.ScanReadForTesting = () =>
    {
        scanning = true;
        activeCancellation.Cancel();
    };
    try { await parallelStore.SearchBatch(["LadderComponent", "LadderStepLength"], ct: activeCancellation.Token); throw new Exception("Active search ignored cancellation"); }
    catch (OperationCanceledException) { Console.WriteLine("PASS active parallel cancellation retains cancellation error type"); }
    finally { parallelStore.ScanReadForTesting = null; }
    Check(scanning, "parallel worker read corpus before cancellation");
}
using (var released = new FileStream(parallelContent, FileMode.Open, FileAccess.Read, FileShare.None))
    Check(released.Length > 0, "cancelled workers release corpus handle");
var recovered = Json(await parallelStore.SearchText("LadderComponent", "resource11.et", 109_998, 10,
    parallelStatus.GetProperty("revision").GetString(), default));
Check(recovered.GetProperty("total").GetInt32() == 110_000 && Hits(recovered).Length == 2,
    "search recovers after cancellation and preserves deep pagination");
Check(recovered.GetProperty("files").GetArrayLength() == 1 && recovered.GetProperty("files")[0].GetProperty("matches").GetArrayLength() == 2,
    "multiple hits share one file identity and hash");
var sameFileBatch = Json(await parallelStore.SearchBatch(["LadderComponent"], pathQuery: "resource11.et"));
var group = sameFileBatch.GetProperty("groups")[0];
var continuation = Json(await parallelStore.SearchText("LadderComponent", "resource11.et", group.GetProperty("nextOffset").GetInt32(), 2,
    parallelStatus.GetProperty("revision").GetString(), default));
Check(group.GetProperty("files").GetArrayLength() == 1 && Hits(group).Length == 3 &&
    Hits(continuation)[0].GetProperty("line").GetInt32() == Hits(group)[2].GetProperty("line").GetInt32() + 1 &&
    Hits(continuation)[0].GetProperty("entryId").GetString() == Hits(group)[0].GetProperty("entryId").GetString(),
    "batch continuation inside one file preserves matching-line offsets");
var empty = Json(await parallelStore.SearchText("nonexistent-marker", "resource11.et", 0, 10, null, default));
Check(empty.GetProperty("files").GetArrayLength() == 0 && empty.GetProperty("total").GetInt32() == 0,
    "empty search returns empty file groups");
Check(!result.TryGetProperty("items", out _) && result.GetProperty("files").EnumerateArray().All(file =>
    file.GetProperty("matches").EnumerateArray().All(hit => !hit.TryGetProperty("entryId", out _) && !hit.TryGetProperty("sha256", out _))),
    "response has one authoritative grouped representation");
Console.WriteLine("Evidence: " + root);

// Small PAC1 fixture with alternating raw/zlib entries exercises the production archive reader.
static void WritePak(string path, Dictionary<string, byte[]> files)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    void Tag(string value) => writer.Write(Encoding.ASCII.GetBytes(value));
    void Be(uint value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(bytes, value); writer.Write(bytes); }
    Tag("FORM"); Be(0); Tag("PAC1"); Tag("DATA"); var sizeAt = stream.Position; Be(0);
    var records = new List<(string Name, uint Offset, uint Stored, uint Original, uint Compression)>();
    int index = 0;
    foreach (var (name, bytes) in files)
    {
        byte[] payload = bytes; uint compression = 0;
        if (index++ % 2 != 0)
        {
            using var buffer = new MemoryStream();
            using (var zlib = new ZLibStream(buffer, CompressionLevel.Fastest, true)) zlib.Write(bytes);
            payload = buffer.ToArray(); compression = 262;
        }
        records.Add((name, (uint)stream.Position, (uint)payload.Length, (uint)bytes.Length, compression));
        writer.Write(payload);
    }
    var dataEnd = stream.Position; stream.Position = sizeAt; Be((uint)(dataEnd - sizeAt - 4)); stream.Position = dataEnd;
    Tag("FILE"); var tableSizeAt = stream.Position; Be(0);
    writer.Write((byte)0); writer.Write((byte)0); writer.Write((uint)records.Count);
    foreach (var row in records)
    {
        var name = Encoding.UTF8.GetBytes(row.Name); writer.Write((byte)1); writer.Write((byte)name.Length); writer.Write(name);
        writer.Write(row.Offset); writer.Write(row.Stored); writer.Write(row.Original); writer.Write(0u); Be(row.Compression); writer.Write(0u);
    }
    var end = stream.Position; stream.Position = tableSizeAt; Be((uint)(end - tableSizeAt - 4)); stream.Position = 4; Be((uint)(end - 8));
}
