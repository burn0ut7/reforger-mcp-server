using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

namespace ReforgerMcp;

public record BinaryIdentity(string Target, string Path, string Version, string Sha256, string AnalysisKey);
public record AnalysisManifest(BinaryIdentity Identity, string ProjectDirectory, DateTimeOffset PreparedAt,
    string GhidraVersion, string BridgeSha256, int FunctionCount, int WarningLines, int ErrorLines, string EvidenceDirectory);
public record JobInfo(string Id, string Target, string State, DateTimeOffset StartedAt, string EvidenceDirectory,
    string? Progress = null, string? Error = null, AnalysisManifest? Analysis = null);
public record EngineResult(BinaryIdentity Identity, string GhidraVersion, string AdapterSha256, int AnalysisWarningLines, int AnalysisErrorLines, string EvidenceDirectory, JsonElement Data);

// Owns binary identity, persistent analysis, jobs and navigation. No model-provider dependencies.
public sealed class NativeEngine : IAsyncDisposable, IDisposable
{
    private readonly Configuration config;
    private readonly GhidraProcess ghidra;
    private readonly CancellationToken stopping;
    private readonly FileStream ownership;
    private readonly SemaphoreSlim execution = new(1);
    private readonly ConcurrentDictionary<string, Job> jobs = new();
    private readonly object sync = new();
    private int disposed;
    private sealed class Job(JobInfo info, CancellationToken stopping)
    {
        public JobInfo Info = info;
        public CancellationTokenSource Cancel = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        public Task Task = Task.CompletedTask;
    }

    public NativeEngine(Configuration config, GhidraProcess ghidra, IHostApplicationLifetime lifetime)
    {
        this.config = config; this.ghidra = ghidra; stopping = lifetime.ApplicationStopping;
        Directory.CreateDirectory(config.DataDirectory);
        try { ownership = new FileStream(Path.Combine(config.DataDirectory, "server.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new McpException("Another server owns this dataDirectory. Use one server process per analysis store."); }
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private async Task<BinaryIdentity> Identity(string target, CancellationToken ct)
    {
        if (!config.Targets.TryGetValue(target, out var path)) throw new McpException("Unknown target. Call engine_targets for configured IDs.");
        if (!File.Exists(path)) throw new McpException("Configured executable is missing: " + path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using (var pe = new PEReader(stream, PEStreamOptions.LeaveOpen))
            if (pe.PEHeaders.CoffHeader.Machine != Machine.Amd64 || pe.PEHeaders.PEHeader?.Magic != PEMagic.PE32Plus || pe.PEHeaders.CorHeader is not null)
                throw new McpException("Target must be native AMD64 PE32+, not a managed assembly.");
        stream.Position = 0;
        var sha = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        var key = Hash($"native-v1|{sha}|{Configuration.GhidraVersion}|{config.AnalysisCpuCount}|{config.AnalysisTimeoutSeconds}");
        return new(target, path, FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unknown", sha, key);
    }
    private string ManifestPath(string target) => Path.Combine(config.DataDirectory, target + ".json");
    private AnalysisManifest? Manifest(string target) => File.Exists(ManifestPath(target))
        ? JsonSerializer.Deserialize<AnalysisManifest>(File.ReadAllText(ManifestPath(target)), Configuration.Json) : null;
    private static bool ProjectExists(AnalysisManifest manifest) => File.Exists(Path.Combine(manifest.ProjectDirectory,"Engine.gpr"))
        && Directory.Exists(Path.Combine(manifest.ProjectDirectory,"Engine.rep"));
    // Persist the preparation profile; expose a revision-bound token so same-byte rebuilds invalidate pages too.
    private static string RevisionKey(AnalysisManifest manifest) => Hash($"{manifest.Identity.AnalysisKey}|{manifest.ProjectDirectory}|{manifest.PreparedAt:O}");
    private static BinaryIdentity EvidenceIdentity(BinaryIdentity identity, AnalysisManifest manifest) => identity with { AnalysisKey = RevisionKey(manifest) };
    private static AnalysisManifest EvidenceManifest(AnalysisManifest manifest) => manifest with { Identity = EvidenceIdentity(manifest.Identity,manifest) };
    private static JobInfo EvidenceJob(JobInfo job) => job.Analysis is { } manifest ? job with { Analysis = EvidenceManifest(manifest) } : job;
    private bool IsStillCurrent(string target, AnalysisManifest selected)
    {
        var current = Manifest(target);
        return current is not null && RevisionKey(current) == RevisionKey(selected);
    }
    private static void Save(string path, object data)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, Configuration.Json));
        File.Move(temp, path, true);
    }
    public async Task<object> Targets(CancellationToken ct)
    {
        var targets = new List<object>();
        foreach (var target in config.Targets.Keys)
        {
            try
            {
                var identity = await Identity(target, ct); var manifest = Manifest(target);
                var active = jobs.Values.FirstOrDefault(j => j.Info.Target == target && j.Info.State is "queued" or "running");
                var current = manifest?.Identity.AnalysisKey == identity.AnalysisKey;
                targets.Add(new { identity = current ? EvidenceIdentity(identity,manifest!) : identity,
                    state = active?.Info.State ?? (manifest is null ? "not_prepared" : !ProjectExists(manifest) ? "missing_project" : current ? "ready" : "stale"),
                    jobId = active?.Info.Id, analysis = manifest is null ? null : EvidenceManifest(manifest) });
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { targets.Add(new { target, state = "unavailable", error = ex.Message }); }
        }
        return new { targets, backend = "Ghidra " + Configuration.GhidraVersion, scope = "Native engine evidence only; not Enforce API declarations, PAK contents or live game state." };
    }
    public async Task<JobInfo> Prepare(string target, CancellationToken ct, bool rebuild = false)
    {
        ghidra.CheckRuntime();
        var identity = await Identity(target, ct);
        lock (sync)
        {
            var active = jobs.Values.FirstOrDefault(j => j.Info.Target == target && j.Info.State is "queued" or "running");
            if (active is not null) return active.Info;
            var manifest = rebuild ? null : Manifest(target);
            if (!rebuild && manifest?.Identity.AnalysisKey == identity.AnalysisKey && ProjectExists(manifest))
                return new("", target, "ready", manifest.PreparedAt, manifest.EvidenceDirectory, Analysis: EvidenceManifest(manifest));
            var id = Guid.NewGuid().ToString("N");
            var folder = Path.Combine(config.DataDirectory, "jobs", id);
            Directory.CreateDirectory(folder);
            var job = new Job(new(id, target, "queued", DateTimeOffset.UtcNow, folder), stopping);
            Save(Path.Combine(folder, "job.json"), job.Info);
            if (rebuild && File.Exists(ManifestPath(target)))
                File.Move(ManifestPath(target),Path.Combine(folder,"previous-manifest.json"));
            jobs[id] = job;
            job.Task = Task.Run(() => PrepareCore(job, identity));
            return EvidenceJob(job.Info);
        }
    }
    private async Task PrepareCore(Job job, BinaryIdentity identity)
    {
        var folder = job.Info.EvidenceDirectory;
        bool entered = false;
        try
        {
            await execution.WaitAsync(job.Cancel.Token); entered = true;
            job.Info = job.Info with { State = "running", Progress = "Copying and validating the installed binary" };
            Save(Path.Combine(folder, "job.json"), job.Info);
            var snapshot = Path.Combine(folder, "Engine.exe");
            using (var source = new FileStream(identity.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = File.Create(snapshot)) await source.CopyToAsync(output, job.Cancel.Token);
            using (var input = File.OpenRead(snapshot))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(input, job.Cancel.Token)).Equals(identity.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new McpException("Installed binary changed while preparing; no analysis was accepted. Request preparation again for its new identity.");
            var project = Path.Combine(folder, "project");
            var result = await ghidra.RunAsync(folder, project, snapshot,
                new { operation = "prepare", sha256 = identity.Sha256, offset = 0, limit = 1 },
                line => job.Info = job.Info with { Progress = line }, job.Cancel.Token);
            if (!result.GetProperty("analysisCompleted").GetBoolean() || !File.Exists(Path.Combine(project,"Engine.gpr")))
                throw new McpException("Analysis did not produce a complete saved project.");
            if ((await Identity(identity.Target, job.Cancel.Token)).AnalysisKey != identity.AnalysisKey)
                throw new McpException("Installed binary changed during analysis. This result is not current; prepare the new build.");
            var lines = File.ReadLines(Path.Combine(folder,"console.log"));
            var warnings = lines.Count(l => l.Contains("WARN ")); var errors = lines.Count(l => l.Contains("ERROR "));
            var bridgeSha = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(ghidra.BridgePath, job.Cancel.Token)));
            var manifest = new AnalysisManifest(identity, project, DateTimeOffset.UtcNow, Configuration.GhidraVersion, bridgeSha,
                result.GetProperty("functionCount").GetInt32(), warnings, errors, folder);
            job.Cancel.Token.ThrowIfCancellationRequested();
            Save(ManifestPath(identity.Target), manifest);
            job.Info = job.Info with { State = "ready", Progress = "Analysis saved; warnings and errors remain available in the evidence log.", Analysis = manifest };
        }
        catch (OperationCanceledException) { job.Info = job.Info with { State = "cancelled", Error = "Cancelled; incomplete analysis was not published." }; }
        catch (Exception ex) { job.Info = job.Info with { State = "failed", Error = ex.Message }; }
        finally
        {
            try { Save(Path.Combine(folder,"job.json"),job.Info); }
            catch (Exception ex) { job.Info = job.Info with { Error = $"{job.Info.Error} Job status could not be persisted: {ex.Message}".Trim() }; }
            finally { if (entered) execution.Release(); }
        }
    }
    public JobInfo JobStatus(string id, bool cancel)
    {
        if (!Guid.TryParseExact(id,"N",out _)) throw new McpException("Invalid job ID.");
        if (jobs.TryGetValue(id, out var job))
        {
            if (cancel && job.Info.State is "queued" or "running") job.Cancel.Cancel();
            return EvidenceJob(job.Info);
        }
        var path = Path.Combine(config.DataDirectory,"jobs",id,"job.json");
        if (!File.Exists(path)) throw new McpException("Unknown job ID.");
        var saved = JsonSerializer.Deserialize<JobInfo>(File.ReadAllText(path),Configuration.Json)!;
        return saved.State is "running" or "queued" ? saved with { State = "interrupted", Error = "Server stopped before this job completed. No automatic retry. Call engine_prepare explicitly." } : EvidenceJob(saved);
    }
    public async Task<object> Diagnostics(string target, int offset, int limit, string? expectedAnalysisKey, CancellationToken ct)
    {
        if (offset is < 0 or > 1_000_000 || limit is < 1 or > 100) throw new McpException("Use offset 0-1000000 and limit 1-100.");
        var identity = await Identity(target,ct);
        var manifest = Manifest(target);
        if (manifest is null || manifest.Identity.AnalysisKey != identity.AnalysisKey)
            throw new McpException("Current analysis is missing or stale; prepare this target first.");
        if (expectedAnalysisKey is not null && expectedAnalysisKey != RevisionKey(manifest))
            throw new McpException("Analysis changed since the previous page. Restart diagnostics with the new identity.");
        var items = new List<object>(); int total = 0; int lineNumber = 0;
        foreach (var line in File.ReadLines(Path.Combine(manifest.EvidenceDirectory,"console.log")))
        {
            ct.ThrowIfCancellationRequested(); lineNumber++;
            if (!line.Contains("ERROR ") && !line.Contains("WARN ")) continue;
            if (total >= offset && items.Count < limit)
                items.Add(new { line = lineNumber, text = line.Length > 1500 ? line[..1500] : line, truncated = line.Length > 1500 });
            total++;
        }
        if ((await Identity(target,ct)).AnalysisKey != identity.AnalysisKey) throw new McpException("Installed binary changed during diagnostics. Result discarded.");
        if (!IsStillCurrent(target,manifest)) throw new McpException("Analysis changed during diagnostics. Result discarded.");
        return new { identity = EvidenceIdentity(identity,manifest), items, total, offset, nextOffset = offset + items.Count < total ? (int?)(offset + items.Count) : null,
            caution = "Logged analysis warnings/errors. Their presence or absence is not a semantic correctness certificate; per-function warnings are also returned by overview/code." };
    }
    public async Task<EngineResult> Query(string target, string operation, string query, string kind, string address,
        string section, int offset, int limit, string? expectedAnalysisKey, CancellationToken ct)
    {
        if (offset is < 0 or > 1_000_000 || limit is < 1 or > 100) throw new McpException("Use offset 0-1000000 and limit 1-100.");
        if (operation == "search" && (string.IsNullOrWhiteSpace(query) || query.Length > 200 || kind is not ("strings" or "symbols")))
            throw new McpException("Use a 1-200 character query and kind strings or symbols.");
        if (operation is "function" or "references" && (!System.Text.RegularExpressions.Regex.IsMatch(address,"\\A(?:0x)?[0-9a-fA-F]{1,16}\\z")
            || operation == "function" && !new[] { "overview", "code", "context", "callers", "callees", "instructions", "types" }.Contains(section)))
            throw new McpException("Use a hexadecimal address and a documented function section.");
        if (!await execution.WaitAsync(0,ct)) throw new McpException("Analysis engine is busy. Check engine_job for preparation progress, or wait for the current query to finish.");
        try
        {
            var identity = await Identity(target,ct);
            var manifest = Manifest(target);
            if (manifest is null || manifest.Identity.AnalysisKey != identity.AnalysisKey)
                throw new McpException("Target analysis is missing or stale. Call engine_prepare, then poll engine_job until ready.");
            if (expectedAnalysisKey is not null && expectedAnalysisKey != RevisionKey(manifest)) throw new McpException("Analysis changed since the previous page. Restart the search with the new identity.");
            if (!ProjectExists(manifest)) throw new McpException("Saved analysis project is missing. Call engine_prepare to explicitly rebuild it.");
            var folder = Path.Combine(config.DataDirectory,"queries",Guid.NewGuid().ToString("N"));
            var response = await ghidra.RunAsync(folder,manifest.ProjectDirectory,null,
                new { operation, query, kind, address, section, offset, limit, sha256 = identity.Sha256 },null,ct);
            if ((await Identity(target,ct)).AnalysisKey != identity.AnalysisKey) throw new McpException("Installed binary changed during query. Result discarded; prepare the new build.");
            if (!IsStillCurrent(target,manifest)) throw new McpException("Analysis changed during query. Result discarded; restart retrieval when preparation is ready.");
            var adapterSha = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(ghidra.BridgePath,ct)));
            return new(EvidenceIdentity(identity,manifest),Configuration.GhidraVersion,adapterSha,manifest.WarningLines,manifest.ErrorLines,folder,response);
        }
        finally { execution.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        foreach (var job in jobs.Values) job.Cancel.Cancel();
        await Task.WhenAll(jobs.Values.Select(j => j.Task));
        foreach (var job in jobs.Values) job.Cancel.Dispose();
        ownership.Dispose(); execution.Dispose();
    }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
