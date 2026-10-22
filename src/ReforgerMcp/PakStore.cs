using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ReforgerMcp;

public sealed record PakIdentity(string Source, string Archive, long Length, long LastWriteUtcTicks, string Sha256);
public sealed record PakScript(string EntryId, string Sha256, string? TextError);
public sealed record PakSnapshot(int Format, string Revision, DateTimeOffset PreparedAt,
    Dictionary<string, string> Roots, List<PakIdentity> Archives, List<PakEntry> Entries, List<PakScript> Scripts)
{
    public Dictionary<string, string> LooseRoots { get; init; } = [];
    public List<LooseIdentity> LooseFiles { get; init; } = [];
    public ProjectBinding? Project { get; init; }
    public ProjectAddon[] Addons { get; init; } = [];
}
public sealed record LooseIdentity(string Source, string Path, long Length, long LastWriteUtcTicks, string Sha256);
public sealed record PakPointer(string Revision, string ManifestSha256);

// Owns all PAK operations and derived files. A revision becomes visible only after a complete
// catalogue and byte-preserving script mirror have been saved. No partial or stale fallback.
public sealed class PakStore(Configuration config, ProjectContext? projects = null)
{
    private readonly SemaphoreSlim gate = new(1);
    private PakSnapshot? cached;
    private PakPointer? cachedPointer;
    private string Store => Path.Combine(config.DataDirectory, "paks");
    private string Current => Path.Combine(Store, "current.json");
    private string RevisionDirectory(string revision) => Path.Combine(Store, revision);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string HashFile(string path)
    {
        using var file = Open(path);
        return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
    }
    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    internal static void NoLinks(string path, HashSet<string>? checkedPaths = null)
    {
        for (var item = new DirectoryInfo(Path.GetFullPath(path)); item is not null; item = item.Parent)
        {
            if (checkedPaths?.Contains(item.FullName) == true) break;
            try
            {
                if ((File.GetAttributes(item.FullName) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked paths are unsupported: " + item.FullName);
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            checkedPaths?.Add(item.FullName);
        }
    }
    private void Configured()
    {
        NoLinks(Store);
        foreach (var root in config.PakDirectories.Values) NoLinks(root);
        foreach (var root in config.UnpackedDirectories.Values) NoLinks(root);
    }
    private static string Output(string root, string relative, HashSet<string>? checkedPaths = null)
    {
        PakArchive.ValidatePath(relative);
        string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Output escapes its destination.");
        NoLinks(full, checkedPaths);
        return full;
    }
    private static async Task Save(string path, object value, CancellationToken ct)
    {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(output, value, Configuration.Json, ct);
    }
    private async Task<T> Run<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) throw new InvalidOperationException("PAK operation busy. Wait for the current request.");
        try { Configured(); return await action(); }
        finally { gate.Release(); }
    }
    private List<(string Source, string Name, string FullPath)> Files(CancellationToken ct = default,
        Dictionary<string, string>? roots = null, bool loose = false, bool isolateRoots = false)
    {
        var files = new List<(string Source, string Name, string FullPath)>();
        roots ??= config.PakDirectories;
        var rootSet = roots.Values.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, root) in roots.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!Directory.Exists(root)) throw new IOException("PAK source unavailable: " + root);
            var pending = new Stack<(string Path, int Depth)>();
            pending.Push((root, 0));
            var paths = new List<string>();
            while (pending.TryPop(out var folder))
            {
                ct.ThrowIfCancellationRequested();
                NoLinks(folder.Path);
                if (folder.Depth > 64) throw new InvalidDataException("PAK source directory depth exceeds 64.");
                foreach (var path in Directory.EnumerateFiles(folder.Path))
                    if (Path.GetExtension(path).Equals(".pak", StringComparison.OrdinalIgnoreCase) != loose) paths.Add(path);
                foreach (var child in Directory.EnumerateDirectories(folder.Path))
                    if (Path.GetFileName(child) is not (".git" or ".svn") && (!isolateRoots || !rootSet.Contains(Path.GetFullPath(child)))) pending.Push((child, folder.Depth + 1));
                if (paths.Count > 2_000_000) throw new InvalidDataException("Source inventory exceeds two million files.");
            }
            paths.Sort(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                ct.ThrowIfCancellationRequested();
                NoLinks(path);
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                PakArchive.ValidatePath(relative);
                files.Add((source, relative, path));
            }
        }
        return files;
    }
    private void Fresh(PakSnapshot snapshot, CancellationToken ct = default)
    {
        if (snapshot.Project is not null)
            (projects ?? throw new InvalidOperationException("Project context service is unavailable.")).Validate(snapshot.Project, ct).GetAwaiter().GetResult();
        else if (!snapshot.Roots.OrderBy(p => p.Key).SequenceEqual(config.PakDirectories.OrderBy(p => p.Key)) ||
            !snapshot.LooseRoots.OrderBy(p => p.Key).SequenceEqual(config.UnpackedDirectories.OrderBy(p => p.Key)))
            throw new InvalidOperationException("PAK configuration changed; run pak_prepare.");
        var files = Files(ct, snapshot.Roots, isolateRoots: snapshot.Project is not null);
        if (files.Count != snapshot.Archives.Count) throw new InvalidOperationException("PAK catalogue stale: archive set changed; run pak_prepare.");
        for (int i = 0; i < files.Count; i++)
        {
            var file = new FileInfo(files[i].FullPath); var saved = snapshot.Archives[i];
            if (files[i].Source != saved.Source || files[i].Name != saved.Archive ||
                file.Length != saved.Length || file.LastWriteTimeUtc.Ticks != saved.LastWriteUtcTicks)
                throw new InvalidOperationException("PAK catalogue stale: archive identity changed; run pak_prepare.");
        }
        var loose = Files(ct, snapshot.LooseRoots, true, snapshot.Project is not null);
        if (loose.Count != snapshot.LooseFiles.Count) throw new InvalidOperationException("Source catalogue stale: loose file set changed; run pak_prepare.");
        for (int i = 0; i < loose.Count; i++)
        {
            var file = new FileInfo(loose[i].FullPath); var saved = snapshot.LooseFiles[i];
            if (loose[i].Source != saved.Source || loose[i].Name != saved.Path || file.Length != saved.Length || file.LastWriteTimeUtc.Ticks != saved.LastWriteUtcTicks ||
                ((file.Extension.Equals(".c", StringComparison.OrdinalIgnoreCase) || file.Extension.Equals(".gproj", StringComparison.OrdinalIgnoreCase)) && HashFile(file.FullName) != saved.Sha256))
                throw new InvalidOperationException("Source catalogue stale: loose file changed; run pak_prepare: " + loose[i].Name);
        }
    }
    private PakSnapshot Load(string? revision, CancellationToken ct = default)
    {
        NoLinks(Current);
        if (!File.Exists(Current)) throw new InvalidOperationException("PAK catalogue not prepared; run pak_prepare.");
        var pointer = JsonSerializer.Deserialize<PakPointer>(File.ReadAllText(Current), Configuration.Json)
            ?? throw new InvalidDataException("Invalid PAK pointer.");
        if (!Guid.TryParseExact(pointer.Revision, "N", out _)) throw new InvalidDataException("Invalid PAK revision.");
        var path = Path.Combine(RevisionDirectory(pointer.Revision), "manifest.json");
        NoLinks(path);
        if (cached is null || cachedPointer != pointer)
        {
            if (HashFile(path) != pointer.ManifestSha256) throw new InvalidDataException("PAK manifest damaged; run pak_prepare.");
            using var file = Open(path);
            cached = JsonSerializer.Deserialize<PakSnapshot>(file, Configuration.Json) ?? throw new InvalidDataException("Invalid PAK manifest.");
            if (cached.Format is not (2 or 3) || cached.Revision != pointer.Revision) throw new InvalidDataException("Unsupported PAK manifest; run pak_prepare.");
            cachedPointer = pointer;
        }
        if (revision is not null && revision != cached.Revision) throw new InvalidOperationException("PAK revision changed; restart the search.");
        Fresh(cached, ct);
        return cached;
    }
    private static object Summary(PakSnapshot s, string directory) => new
    {
        state = "ready", revision = s.Revision, preparedAt = s.PreparedAt, sources = s.Roots, unpackedSources = s.LooseRoots,
        project = s.Project, addons = s.Addons, looseFileCount = s.LooseFiles.Count,
        archiveCount = s.Archives.Count, entryCount = s.Entries.Count, scriptCount = s.Scripts.Count,
        sourceCoverage = s.Roots.Concat(s.LooseRoots).DistinctBy(p => p.Key).Select(root => new
        {
            source = root.Key, root = root.Value,
            archiveCount = s.Archives.Count(a => a.Source == root.Key),
            looseFileCount = s.LooseFiles.Count(a => a.Source == root.Key),
            entryCount = s.Entries.Count(e => e.Source == root.Key),
            scriptCount = s.Entries.Count(e => e.Source == root.Key && e.Extension == ".c")
        }).ToArray(),
        searchableScripts = s.Scripts.Count(p => p.TextError is null),
        unreadableScripts = s.Scripts.Where(p => p.TextError is not null).ToArray(),
        scriptDirectory = Path.Combine(directory, "scripts"), manifest = Path.Combine(directory, "manifest.json"),
        extensions = s.Entries.GroupBy(e => e.Extension).OrderByDescending(g => g.Count())
            .Select(g => new { extension = g.Key, count = g.Count() }).ToArray(),
        freshness = "Source sets, lengths and modification times checked on every operation; loose scripts/project files also rehashed. Selected loose reads verify captured hashes. Project-bound catalogues reobserve the native context. Disk bytes only, not unsaved editor buffers or effective resource precedence."
    };
    public Task<object> Status(CancellationToken ct) => Run(() =>
    {
        try
        {
            var snapshot = Load(null, ct);
            return Task.FromResult(Summary(snapshot, RevisionDirectory(snapshot.Revision)));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or JsonException)
        { return Task.FromResult<object>(new { state = "unavailable", reason = ex.Message }); }
    }, ct);

    public async Task<object> ResolvePaths(string[] paths, string? source, CancellationToken ct)
    {
        if (paths.Length == 0)
            return new { state = "not-queried", reason = "Selected pages contain no supported file links." };
        if (!File.Exists(Current) && config.PakDirectories.Count == 0 && config.UnpackedDirectories.Count == 0)
            return new { state = "unavailable", reason = "Set pakDirectories and explicitly run pak_prepare." };
        return await Run(() =>
        {
            PakSnapshot s;
            try { s = Load(null, ct); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or JsonException)
            { return Task.FromResult<object>(new { state = "unavailable", reason = ex.Message }); }
            ValidateSource(s, source);
            var requested = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var matches = s.Entries.Where(e => (source is null || e.Source == source) && requested.Contains(e.Path))
                .ToLookup(e => e.Path, StringComparer.OrdinalIgnoreCase);
            Fresh(s, ct);
            return Task.FromResult<object>(new
            {
                state = "ready", s.Revision, s.PreparedAt,
                meaning = "Exact case-insensitive paths. Multiple archives/sources remain distinct; no override precedence inferred. Read files to verify current declarations.",
                files = paths.Select(path => new
                {
                    path, total = matches[path].Count(), omitted = Math.Max(0, matches[path].Count() - 2),
                    candidates = matches[path].Take(2).Select(e => new
                    {
                        entryId = e.Id, e.Source, e.Archive, e.Storage,
                        next = e.Extension == ".c"
                            ? (object)new { tool = "pak_read", arguments = new { entryId = e.Id, revision = s.Revision, offset = 0, limit = 40 } }
                            : new { tool = "pak_extract", arguments = new { entryId = e.Id, revision = s.Revision } }
                    }),
                    search = new { tool = "pak_search", arguments = new { query = path, source, revision = s.Revision, limit = 20 } }
                })
            });
        }, ct);
    }

    public Task<object> Prepare(CancellationToken ct, string? projectToken = null, bool includeAvailable = false, string[]? addonGuids = null) => Run<object>(async () =>
    {
        var roots = new Dictionary<string, string>(config.PakDirectories);
        var looseRoots = new Dictionary<string, string>(config.UnpackedDirectories);
        ProjectBinding? binding = null;
        ProjectAddon[] addons = [];
        if (projectToken is not null)
        {
            var observed = await (projects ?? throw new InvalidOperationException("Project context service unavailable.")).Resolve(projectToken, ct);
            binding = ProjectContext.Binding(observed);
            var selected = ProjectContext.Unique(observed).Where(a => includeAvailable || a.Loaded).ToArray();
            if (addonGuids is not null)
            {
                if (addonGuids.Length is < 1 or > 1024 || addonGuids.Any(g => !selected.Any(a => a.Guid.Equals(g, StringComparison.OrdinalIgnoreCase))))
                    throw new ArgumentException("addonGuids must select observed addons within the requested loaded/available scope.");
                selected = selected.Where(a => addonGuids.Contains(a.Guid, StringComparer.OrdinalIgnoreCase)).ToArray();
            }
            if (selected.Select(a => a.Guid).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length)
                throw new InvalidOperationException("Multiple project locations share an addon GUID; resolve ambiguity before preparing.");
            addons = selected.Select(a => ProjectContext.Describe(a, observed.Context.Addons.Any(x => !x.Loaded && x.Guid == a.Guid && x.ProjectFile == a.ProjectFile))).ToArray();
            if (addons.Any(a => a.DeclarationState != "read")) throw new InvalidOperationException("Selected addon project declarations are unavailable or differ from loaded identities; inspect workbench_project_context.");
            roots = addons.ToDictionary(a => a.Guid, a => a.Root);
            looseRoots = new(roots);
        }
        else if (includeAvailable || addonGuids is not null) throw new ArgumentException("includeAvailable/addonGuids require projectToken.");
        if (roots.Count == 0 && looseRoots.Count == 0) throw new InvalidOperationException("Configure pakDirectories/unpackedDirectories or supply a projectToken.");
        foreach (var root in roots.Values.Concat(looseRoots.Values))
        {
            NoLinks(root);
            var input = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(config.DataDirectory));
            if (input.Equals(output, StringComparison.OrdinalIgnoreCase) || input.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || output.StartsWith(input + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Source roots and dataDirectory must not overlap.");
        }
        NoLinks(Current);
        if (File.Exists(Current)) File.Delete(Current);
        cached = null; cachedPointer = null;
        var files = Files(ct, roots, isolateRoots: binding is not null);
        var loose = Files(ct, looseRoots, true, binding is not null);
        Directory.CreateDirectory(Store);
        var revision = Guid.NewGuid().ToString("N");
        var directory = RevisionDirectory(revision);
        Directory.CreateDirectory(directory);
        // Keep every source handle read-locked until publication, so a multi-archive snapshot
        // cannot combine parts from a concurrent game update.
        var streams = new Dictionary<(string, string), FileStream>();
        try
        {
            foreach (var file in files) streams.Add((file.Source, file.Name), Open(file.FullPath));
            var identities = new List<PakIdentity>();
            var looseIdentities = new List<LooseIdentity>();
            var entries = new List<PakEntry>();
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var stream = streams[(file.Source, file.Name)];
                var sha = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
                identities.Add(new(file.Source, file.Name, stream.Length, File.GetLastWriteTimeUtc(file.FullPath).Ticks, sha));
                entries.AddRange(PakArchive.Catalogue(stream, file.Source, file.Name, ct));
                if (entries.Count > 2_000_000) throw new InvalidDataException("Catalogue exceeds two million entries.");
            }
            foreach (var file in loose)
            {
                ct.ThrowIfCancellationRequested();
                using var stream = Open(file.FullPath);
                if (stream.Length > int.MaxValue) throw new InvalidDataException("Loose file exceeds indexed size bound: " + file.Name);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
                looseIdentities.Add(new(file.Source, file.Name, stream.Length, File.GetLastWriteTimeUtc(file.FullPath).Ticks, hash));
                entries.Add(new(file.Source, "", file.Name, 0, (int)stream.Length, (int)stream.Length, 0, "loose"));
                if (entries.Count > 2_000_000) throw new InvalidDataException("Catalogue exceeds two million entries.");
            }
            var scripts = new List<PakScript>();
            long total = 0;
            foreach (var entry in entries.Where(e => e.Extension == ".c"))
            {
                ct.ThrowIfCancellationRequested();
                total += entry.OriginalBytes;
                if (total > 512L * 1024 * 1024) throw new InvalidDataException("Script mirror exceeds 512 MiB limit.");
                var bytes = entry.Storage == "pak" ? PakArchive.Read(streams[(entry.Source, entry.Archive)], entry, ct)
                    : ReadLoose(Output(looseRoots[entry.Source], entry.Path));
                if (entry.Storage == "loose" && Hash(bytes) != looseIdentities.Single(f => f.Source == entry.Source && f.Path == entry.Path).Sha256)
                    throw new InvalidOperationException("Loose script changed during preparation: " + entry.Id);
                string? error = null;
                try { PakArchive.Text(bytes); }
                catch (Exception ex) when (ex is DecoderFallbackException or InvalidDataException) { error = ex.Message; }
                var path = Output(Path.Combine(directory, "scripts"), entry.Id);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, bytes, ct);
                scripts.Add(new(entry.Id, Hash(bytes), error));
            }
            var snapshot = new PakSnapshot(3, revision, DateTimeOffset.UtcNow,
                roots, identities, entries, scripts) { LooseRoots = looseRoots, LooseFiles = looseIdentities, Project = binding, Addons = addons };
            Fresh(snapshot, ct);
            var manifest = Path.Combine(directory, "manifest.json");
            await Save(manifest, snapshot, ct);
            var pointer = new PakPointer(revision, HashFile(manifest));
            var temp = Path.Combine(Store, revision + ".pointer.tmp");
            await Save(temp, pointer, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temp, Current, true);
            cached = snapshot; cachedPointer = pointer;
            return Summary(snapshot, directory);
        }
        catch
        {
            // No old revision is silently restored after a failed explicit refresh.
            if (File.Exists(Current)) File.Delete(Current);
            cached = null; cachedPointer = null;
            throw;
        }
        finally { foreach (var stream in streams.Values) stream.Dispose(); }
    }, ct);

    private static void Page(int offset, int limit, string? revision)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentException("offset must be nonnegative; limit must be 1–100.");
        if (offset > 0 && revision is null) throw new ArgumentException("Pass revision when paging.");
    }
    private static string[] Terms(string query) => query.Length <= 500
        ? query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        : throw new ArgumentException("Query exceeds 500 characters.");
    private static bool Match(string value, string[] terms) => terms.All(t => value.Contains(t, StringComparison.OrdinalIgnoreCase));
    private static void ValidateSource(PakSnapshot s, string? source)
    {
        if (source is not null && !s.Roots.ContainsKey(source) && !s.LooseRoots.ContainsKey(source))
            throw new ArgumentException("Unknown source; use pak_status sourceCoverage.");
    }
    private static readonly Dictionary<string, string[]> Kinds = new()
    {
        ["all"] = [], ["script"] = [".c"], ["sound"] = [".wav", ".snd"],
        ["audio-config"] = [".acp", ".sig", ".afm", ".smap"], ["prefab"] = [".et"],
        ["config"] = [".conf"], ["layout"] = [".layout"], ["material"] = [".emat"]
    };
    public Task<object> Search(string query, string kind, string? extension, string? source, int offset, int limit,
        string? revision, CancellationToken ct) => Run(() =>
    {
        Page(offset, limit, revision);
        if (!Kinds.TryGetValue(kind, out var extensions)) throw new ArgumentException("Unknown kind: " + string.Join(", ", Kinds.Keys));
        var terms = Terms(query);
        var s = Load(revision, ct);
        ValidateSource(s, source);
        var hits = s.Entries.Where(e => (source is null || e.Source == source) && Match(e.Path, terms) &&
            (extensions.Length == 0 || extensions.Contains(e.Extension)) &&
            (extension is null || e.Extension.Equals("." + extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase))).ToArray();
        var items = hits.Skip(offset).Take(limit).ToArray();
        Fresh(s, ct);
        return Task.FromResult<object>(new { revision = s.Revision, total = hits.Length, offset,
            nextOffset = offset + items.Length < hits.Length ? (int?)(offset + items.Length) : null, items });
    }, ct);

    private PakEntry Entry(PakSnapshot snapshot, string id) => snapshot.Entries.SingleOrDefault(e => e.Id == id)
        ?? throw new ArgumentException("Unknown entryId. Use an exact ID returned by pak_search.");
    private byte[] Read(PakSnapshot snapshot, PakEntry entry, CancellationToken ct)
    {
        if (entry.Storage == "loose")
        {
            var bytes = ReadLoose(Output(snapshot.LooseRoots[entry.Source], entry.Path));
            if (Hash(bytes) != snapshot.LooseFiles.Single(f => f.Source == entry.Source && f.Path == entry.Path).Sha256)
                throw new InvalidOperationException("Loose file changed; run pak_prepare: " + entry.Id);
            return bytes;
        }
        var path = Path.Combine(snapshot.Roots[entry.Source], entry.Archive);
        NoLinks(path);
        using var file = Open(path);
        return PakArchive.Read(file, entry, ct);
    }
    private static byte[] ReadLoose(string path)
    {
        using var file = Open(path);
        if (file.Length > PakArchive.MaxPayload) throw new InvalidDataException("Loose read exceeds 64 MiB.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes); return bytes;
    }
    private static object[] Lines(string text, int offset, int limit, out int total)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        total = lines.Length;
        return lines.Skip(offset).Take(limit).Select((line, i) => (object)new
        { line = offset + i + 1, text = line[..Math.Min(line.Length, 2000)], truncated = line.Length > 2000 }).ToArray();
    }
    public Task<object> ReadText(string entryId, int offset, int limit, string? revision, CancellationToken ct) => Run(() =>
    {
        Page(offset, limit, revision);
        var s = Load(revision, ct); var entry = Entry(s, entryId);
        var bytes = Read(s, entry, ct);
        var lines = Lines(PakArchive.Text(bytes), offset, limit, out int total);
        Fresh(s, ct);
        return Task.FromResult<object>(new { revision = s.Revision, entry, sha256 = Hash(bytes),
            evidence = entry.Extension == ".c" ? "Existing Enforce source; proto external declarations do not contain native bodies." : "Archive text; no resource conversion.",
            total, offset, nextOffset = offset + lines.Length < total ? (int?)(offset + lines.Length) : null, lines });
    }, ct);

    private const string ScriptSearchScope = "Literal, case-insensitive, all terms on one line. Scripts only; not semantic symbol resolution or a complete resource dependency graph.";
    public Task<object> SearchText(string query, string pathQuery, int offset, int limit, string? revision,
        CancellationToken ct, string? source = null) =>
        ScanScripts([query], pathQuery, offset, limit, revision, ct, source, false);

    public Task<object> SearchBatch(string[] queries, string pathQuery = "", string? source = null,
        string? revision = null, CancellationToken ct = default) =>
        ScanScripts(queries, pathQuery, 0, 3, revision, ct, source, true);

    private Task<object> ScanScripts(string[] queries, string pathQuery, int offset, int limit, string? revision,
        CancellationToken ct, string? source, bool batch) => Run(() =>
    {
        Page(offset, limit, revision);
        if (queries is null || queries.Length is < 1 or > 8)
            throw new ArgumentException("Supply 1–8 queries.");
        if (queries.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Script content queries must not be empty.");
        var terms = queries.Select(Terms).ToArray();
        var paths = Terms(pathQuery);
        var candidates = SearchValues.Create(terms.SelectMany(t => t).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            StringComparison.OrdinalIgnoreCase);
        var s = Load(revision, ct);
        ValidateSource(s, source);
        var items = queries.Select(_ => new List<object>()).ToArray();
        var totals = new int[queries.Length];
        int searched = 0, excluded = 0;
        var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in s.Scripts.Where(e =>
            (source is null || e.EntryId.StartsWith(source + "/", StringComparison.Ordinal)) && Match(e.EntryId, paths)))
        {
            ct.ThrowIfCancellationRequested();
            if (script.TextError is not null) { excluded++; continue; }
            var path = Output(Path.Combine(RevisionDirectory(s.Revision), "scripts"), script.EntryId, checkedPaths);
            using var file = Open(path);
            if (file.Length > PakArchive.MaxPayload) throw new InvalidDataException("Script mirror exceeds payload limit: " + script.EntryId);
            var bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            if (Hash(bytes) != script.Sha256) throw new InvalidDataException("Script mirror changed; run pak_prepare: " + script.EntryId);
            // Preserve the existing CR/LF definition of a source line. EnumerateLines
            // also splits Unicode separators, changing match and line-number semantics.
            var text = PakArchive.Text(bytes).Replace("\r\n", "\n").Replace('\r', '\n');
            searched++;
            int lineNumber = 0;
            foreach (var range in text.AsSpan().Split('\n'))
            {
                var line = text.AsSpan()[range];
                lineNumber++;
                if ((lineNumber & 255) == 0) ct.ThrowIfCancellationRequested();
                if (!line.ContainsAny(candidates)) continue;
                for (int q = 0; q < terms.Length; q++)
                {
                    bool match = true;
                    foreach (var term in terms[q])
                        if (!line.Contains(term, StringComparison.OrdinalIgnoreCase)) { match = false; break; }
                    if (!match) continue;
                    if (totals[q] >= offset && items[q].Count < limit)
                    {
                        int max = batch ? 180 : 2000;
                        items[q].Add(new { entryId = script.EntryId, line = lineNumber,
                            text = line[..Math.Min(line.Length, max)].ToString(), truncated = line.Length > max, sha256 = script.Sha256 });
                    }
                    totals[q]++;
                }
            }
        }
        Fresh(s, ct);
        if (!batch)
            return Task.FromResult<object>(new { revision = s.Revision, total = totals[0], offset,
                nextOffset = offset + items[0].Count < totals[0] ? (int?)(offset + items[0].Count) : null,
                searchedScripts = searched, excludedUnreadableScripts = excluded, scope = ScriptSearchScope, items = items[0] });
        return Task.FromResult<object>(new { revision = s.Revision, searchedScripts = searched,
            excludedUnreadableScripts = excluded, scope = ScriptSearchScope,
            continuation = "For more matches, call pak_search_text with the group's query, nextOffset as offset, this revision, and the same source/pathQuery.",
            groups = queries.Select((query, q) => new { query, total = totals[q],
                nextOffset = items[q].Count < totals[q] ? (int?)items[q].Count : null, items = items[q] }).ToArray() });
    }, ct);

    public Task<object> Extract(string entryId, string? revision, CancellationToken ct) => Run<object>(async () =>
    {
        var s = Load(revision, ct); var entry = Entry(s, entryId);
        var bytes = Read(s, entry, ct);
        var directory = Path.Combine(Store, "exports", Guid.NewGuid().ToString("N"));
        var path = Output(directory, entry.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);
        Fresh(s, ct);
        var result = new { revision = s.Revision, entry, sha256 = Hash(bytes), file = path,
            archive = s.Archives.SingleOrDefault(a => a.Source == entry.Source && a.Archive == entry.Archive),
            looseFile = entry.Storage == "loose" ? s.LooseFiles.Single(f => f.Source == entry.Source && f.Path == entry.Path) : null,
            meaning = "Original file bytes exported without transformation." };
        await Save(Path.Combine(directory, "provenance.json"), result, ct);
        return result;
    }, ct);
}
