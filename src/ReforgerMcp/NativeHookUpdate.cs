using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReforgerMcp;

// Offline maintainer workflow. Never loads a candidate into Workbench or changes a hash guard.
public static class NativeHookUpdate
{
    public sealed record Request(string SourceRoot, string BaselineManifest, string CandidateManifest, string OutputDirectory);
    public sealed record Use(string File, int Line, string Context);
    public sealed record Hook(string Id, string Rva, Use[] Uses);
    public sealed record SourceFile(string Path, string Sha256);

    public static Hook[] Inventory(string root, string? baselineHash = null)
    {
        var uses = new SortedDictionary<int, List<Use>>();
        var profilePath = Path.Combine(root, "src", "ReforgerMcp", "NativeWorkbench.Profile.cs");
        bool limited = false;
        if (File.Exists(profilePath))
        {
            if (baselineHash is null) throw new InvalidDataException("Select the baseline binary hash for a multi-profile source tree.");
            var source = File.ReadAllText(profilePath);
            var profiles = Regex.Matches(source, @"MaintenanceProfile\s+\w+\s*=\s*new\((true|false),\s*""([0-9a-fA-F]{64})""(?<body>[\s\S]*?)\);");
            var selected = profiles.Cast<Match>().SingleOrDefault(m => m.Groups[2].Value.Equals(baselineHash, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Baseline hash has no explicit source profile.");
            limited = selected.Groups[1].Value == "true";
            foreach (Match value in Regex.Matches(selected.Groups["body"].Value, @"0x[0-9a-fA-F]+"))
            {
                int rva = Convert.ToInt32(value.Value[2..], 16);
                int line = source[..(selected.Groups["body"].Index + value.Index)].Count(c => c == '\n') + 1;
                if (!uses.TryGetValue(rva, out var locations)) uses[rva] = locations = [];
                locations.Add(new("src/ReforgerMcp/NativeWorkbench.Profile.cs", line, "Selected exact-binary profile operand " + value.Value));
            }
        }
        if (!limited) foreach (var path in Directory.GetFiles(Path.Combine(root, "src", "ReforgerMcp"), "NativeWorkbench*.cs").Order(StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(path);
            for (int line = 0; line < lines.Length; line++)
            {
                var code = lines[line].Split("//", 2)[0];
                // Deliberately explicit workbench operands. Qt RVAs, flags, object offsets,
                // emitted instruction bytes and comments must not become executable hook guesses.
                var patterns = new[] {
                    @"(?:image|exe\.BaseAddress)\s*\+\s*(0x[0-9a-fA-F]+)",
                    @"CheckCode\(exe,\s*(0x[0-9a-fA-F]+)",
                    @"Call\((0x[0-9a-fA-F]+)\)",
                    @"ResourceLeakSuppressionRva\s*=\s*(0x[0-9a-fA-F]+)"
                };
                var values = patterns.SelectMany(p => Regex.Matches(code, p).Select(m => m.Groups[1].Value)).ToList();
                if (code.Contains("CheckCode(exe, rva)"))
                    values.AddRange(Regex.Matches(code, @"0x[0-9a-fA-F]+").Select(m => m.Value));
                if (code.Contains("Call(command.Action == \"redo\""))
                    values.AddRange(Regex.Matches(code, @"0x[0-9a-fA-F]+").Select(m => m.Value));
                foreach (var value in values.Distinct())
                {
                    int rva = Convert.ToInt32(value[2..], 16);
                    if (!uses.TryGetValue(rva, out var list)) uses[rva] = list = [];
                    list.Add(new(Path.GetRelativePath(root, path).Replace('\\', '/'), line + 1, lines[line].Trim()));
                }
            }
        }
        return uses.Select(p => new Hook("workbench_" + p.Key.ToString("x"), p.Key.ToString("x"), p.Value.ToArray())).ToArray();
    }

    public static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string CandidateSource(JsonElement result, string binaryHash)
    {
        if (!Regex.IsMatch(binaryHash, "\\A[0-9a-fA-F]{64}\\z")) throw new InvalidDataException("Invalid binary hash.");
        var lines = new List<string> { "// RESEARCH CANDIDATES ONLY. Not compiled into the server; not runtime-approved.",
            "// Binary SHA-256: " + binaryHash, "namespace ReforgerMcp.Research;", "internal static class CandidateHooks", "{" };
        foreach (var hook in result.GetProperty("hooks").EnumerateArray())
        {
            var id = hook.GetProperty("id").GetString()!;
            if (!Regex.IsMatch(id, "\\Aworkbench_[0-9a-f]+\\z")) throw new InvalidDataException("Invalid hook ID.");
            if (hook.GetProperty("state").GetString() != "candidate") continue;
            var candidates = hook.GetProperty("candidateRvas");
            if (candidates.GetArrayLength() != 1 || hook.GetProperty("candidateCount").GetInt32() != 1)
                throw new InvalidDataException("Inconsistent candidate count.");
            var rva = candidates[0].GetString()!;
            if (!uint.TryParse(rva, System.Globalization.NumberStyles.HexNumber, null, out _)) throw new InvalidDataException("Invalid candidate RVA.");
            lines.Add($"    internal const uint {id} = 0x{rva};");
        }
        lines.Add("}"); return string.Join("\n", lines) + "\n";
    }

    public static async Task<object> Run(Configuration config, string requestPath, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(requestPath), Configuration.Json)
            ?? throw new InvalidDataException("Missing update request.");
        var root = Path.GetFullPath(request.SourceRoot);
        var output = Path.GetFullPath(request.OutputDirectory);
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output must be a new directory; previous evidence is never overwritten.");
        AnalysisManifest ReadManifest(string path) => JsonSerializer.Deserialize<AnalysisManifest>(File.ReadAllText(path), Configuration.Json)
            ?? throw new InvalidDataException("Invalid analysis manifest.");
        var baseline = ReadManifest(request.BaselineManifest); var candidate = ReadManifest(request.CandidateManifest);
        if (baseline.Identity.Target != "workbench" || candidate.Identity.Target != "workbench") throw new InvalidDataException("Both manifests must describe workbench.");
        foreach (var m in new[] { baseline, candidate })
        {
            if (m.GhidraVersion != Configuration.GhidraVersion || !File.Exists(Path.Combine(m.ProjectDirectory, "Engine.gpr")))
                throw new InvalidDataException("Missing or incompatible saved analysis project.");
            if (!HashFile(Path.Combine(m.EvidenceDirectory, "Engine.exe")).Equals(m.Identity.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Analysis snapshot hash mismatch.");
        }
        var installed = config.Workbench?.Executable ?? throw new InvalidDataException("Workbench must be configured.");
        if (!HashFile(installed).Equals(candidate.Identity.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Candidate analysis does not match installed Workbench.");
        // Profile-aware discovery must never interpret 1.8 editor addresses as 1.9 hooks.
        if (!File.Exists(Path.Combine(root, "src", "ReforgerMcp", "NativeWorkbench.Profile.cs")))
        {
            var dispatcher = File.ReadAllText(Path.Combine(root, "src", "ReforgerMcp", "NativeWorkbench.cs"));
            if (!dispatcher.Contains('"' + baseline.Identity.Sha256.ToLowerInvariant() + '"', StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Baseline binary hash is not present in the source dispatcher guard.");
        }
        var hooks = Inventory(root, baseline.Identity.Sha256);
        if (hooks.Length is < 1 or > 512) throw new InvalidDataException("Expected 1-512 explicit Workbench hooks.");
        Directory.CreateDirectory(output);
        void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, Configuration.Json));
        Save("request.json", request); Save("baseline-manifest.json", baseline); Save("candidate-manifest.json", candidate);
        var sourcePaths = Directory.GetFiles(Path.Combine(root, "src", "ReforgerMcp"), "*.cs", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(root, "adapters"), "*.c", SearchOption.AllDirectories))
            .Append(Path.Combine(root, "src", "ReforgerMcp", "Ghidra", "ReforgerBridge.java")).Order(StringComparer.Ordinal).ToArray();
        var sources = sourcePaths.Select(p => new SourceFile(Path.GetRelativePath(root, p).Replace('\\', '/'), HashFile(p))).ToArray();
        Save("sources.json", sources); Save("inventory.json", hooks);
        var ghidra = new GhidraProcess(config);
        Save("run.json", new { state = "running", algorithm = "masked-function-v1", runtimeApproved = false, bridgeSha256 = HashFile(ghidra.BridgePath) });
        try
        {
            var export = await ghidra.RunAsync(Path.Combine(output, "baseline"), baseline.ProjectDirectory, null,
                new { operation = "hook_export", offset = 0, limit = 1, sha256 = baseline.Identity.Sha256, hooks }, null, ct);
            var matches = await ghidra.RunAsync(Path.Combine(output, "candidate"), candidate.ProjectDirectory, null,
                new { operation = "hook_match", offset = 0, limit = 1, sha256 = candidate.Identity.Sha256, baseline = export }, null, ct);
            if (!HashFile(installed).Equals(candidate.Identity.Sha256, StringComparison.OrdinalIgnoreCase) ||
                sources.Any(s => HashFile(Path.Combine(root, s.Path)) != s.Sha256))
                throw new InvalidDataException("Binary or source changed during discovery; results must not be used.");
            if (baseline.Identity.Sha256.Equals(candidate.Identity.Sha256, StringComparison.OrdinalIgnoreCase))
                foreach (var h in matches.GetProperty("hooks").EnumerateArray())
                    if (h.GetProperty("state").GetString() == "candidate" &&
                        h.GetProperty("candidateRvas")[0].GetString() != h.GetProperty("oldRva").GetString())
                        throw new InvalidDataException("Self-comparison produced an incorrect unique candidate: " + h.GetProperty("id").GetString());
            var counts = matches.GetProperty("hooks").EnumerateArray().GroupBy(h => h.GetProperty("state").GetString()!)
                .ToDictionary(g => g.Key, g => g.Count());
            File.WriteAllText(Path.Combine(output, "CandidateHooks.cs.txt"), CandidateSource(matches, candidate.Identity.Sha256));
            var result = new { state = "review_required", runtimeApproved = false, hookCount = hooks.Length, counts,
                baselineSha256 = baseline.Identity.Sha256, candidateSha256 = candidate.Identity.Sha256,
                outputDirectory = output, algorithm = "masked-function-v1",
                coverage = "Explicit Workbench RVAs only. Qt/system modules, encoded object layouts, virtual slots, calling conventions and live behavior require independent validation. No binary was patched or deployed." };
            Save("run.json", result); return result;
        }
        catch (Exception ex) { Save("run.json", new { state = "failed", runtimeApproved = false, error = ex.Message }); throw; }
    }
}

