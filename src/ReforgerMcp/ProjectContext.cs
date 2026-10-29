using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

internal sealed record ProjectObservation(int ProcessId, long ProcessStarted, string Version, NativeProjectContext Context);
public sealed record ProjectBinding(int ProcessId, long ProcessStarted, string Fingerprint);
public sealed record ProjectAddon(string Guid, string Id, string Title, string ProjectFile, string Root,
    bool Loaded, bool Available, bool Current, string? MountedRoot, string DeclarationState,
    string? DeclarationError, ProjectFileInfo? Declaration);

public sealed class ProjectContext(WorkbenchSession session)
{
    private readonly Dictionary<string, ProjectBinding> tokens = [];
    private readonly object gate = new();
    internal async Task<ProjectObservation> Observe(int processId, CancellationToken ct)
        => (ProjectObservation)await session.ObserveProject(processId, ct);
    internal static ProjectBinding Binding(ProjectObservation observed) => new(observed.ProcessId, observed.ProcessStarted,
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(observed, Configuration.Json))));

    internal async Task<ProjectObservation> Validate(ProjectBinding binding, CancellationToken ct)
    {
        var observed = await Observe(binding.ProcessId, ct);
        if (Binding(observed) != binding)
            throw new InvalidOperationException("Loaded/available project context changed; observe workbench_project_context and prepare again. No old project is substituted.");
        return observed;
    }

    internal async Task<ProjectObservation> Resolve(string token, CancellationToken ct)
    {
        ProjectBinding binding;
        lock (gate)
            if (!tokens.TryGetValue(token, out binding!))
                throw new ArgumentException("Unknown projectToken; call workbench_project_context on this connection.");
        return await Validate(binding, ct);
    }

    internal static NativeProjectAddon[] Unique(ProjectObservation observed) => observed.Context.Addons
        .GroupBy(a => (a.Guid, a.ProjectFile)).Select(g => g.FirstOrDefault(a => a.Loaded) ?? g.First()).ToArray();

    internal static ProjectAddon Describe(NativeProjectAddon addon, bool available)
    {
        if (!Path.IsPathFullyQualified(addon.ProjectFile) || !addon.ProjectFile.EndsWith(".gproj", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native project file is not an absolute .gproj path.");
        string path = Path.GetFullPath(addon.ProjectFile), root = Path.GetDirectoryName(path)!;
        ProjectFileInfo? declaration = null; string state = "read", error = "";
        try
        {
            PakStore.NoLinks(path);
            declaration = ProjectFile.Read(path);
            if (!declaration.Guid.Equals(addon.Guid, StringComparison.OrdinalIgnoreCase) || declaration.Id != addon.Id)
            { state = "identity_mismatch"; error = "On-disk project identity differs from the native owner."; }
            if (addon.MountResolved && !Path.TrimEndingDirectorySeparator(Path.GetFullPath(addon.MountedRoot)).Equals(root, StringComparison.OrdinalIgnoreCase))
            { state = "location_mismatch"; error = "Mounted directory differs from the native project-file directory; source selection is unresolved."; }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
        { state = "unavailable"; error = ex.Message; }
        return new(addon.Guid, addon.Id, addon.Title, path, root, addon.Loaded, available, addon.Current,
            addon.MountResolved ? addon.MountedRoot : null, state, error.Length == 0 ? null : error, declaration);
    }

    public async Task<object> Inspect(int processId, string scope, string query, int offset, int limit, string? projectToken, CancellationToken ct)
    {
        if (scope is not ("loaded" or "available" or "all") || offset < 0 || limit is < 1 or > 100 || query.Length > 500)
            throw new ArgumentException("scope must be loaded, available or all; offset nonnegative, limit 1-100, query at most 500 characters.");
        if (offset > 0 && projectToken is null) throw new ArgumentException("Pass projectToken when paging.");
        var observed = projectToken is null ? await Observe(processId, ct) : await Resolve(projectToken, ct);
        if (observed.ProcessId != processId) throw new ArgumentException("projectToken belongs to a different process.");
        var token = Guid.NewGuid().ToString("N");
        lock (gate)
        {
            if (tokens.Count >= 16) tokens.Remove(tokens.Keys.First());
            tokens.Add(token, Binding(observed));
        }
        var available = observed.Context.Addons.Where(a => !a.Loaded).Select(a => (a.Guid, a.ProjectFile)).ToHashSet();
        var all = Unique(observed);
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var matches = all.Where(a => (scope == "all" || scope == "loaded" && a.Loaded || scope == "available" && available.Contains((a.Guid, a.ProjectFile))) &&
            terms.All(t => (a.Guid + " " + a.Id + " " + a.Title + " " + a.ProjectFile).Contains(t, StringComparison.OrdinalIgnoreCase))).ToArray();
        var items = matches.Skip(offset).Take(limit).Select(a => { ct.ThrowIfCancellationRequested(); return Describe(a, available.Contains((a.Guid, a.ProjectFile))); }).ToArray();
        return new { state = "observed", projectToken = token, observed.ProcessId, observed.ProcessStarted,
            workbenchVersion = observed.Version, observedAt = DateTimeOffset.UtcNow,
            currentProject = all.Single(a => a.Current).ProjectFile,
            totalLoaded = all.Count(a => a.Loaded), totalAvailable = available.Count,
            scope, total = matches.Length, offset, nextOffset = offset + items.Length < matches.Length ? (int?)(offset + items.Length) : null,
            addons = items,
            dependencies = items.SelectMany(a => (a.Declaration?.Dependencies ?? []).Select(guid => new { addon = a.Guid, dependency = guid,
                state = !System.Text.RegularExpressions.Regex.IsMatch(guid, "^[0-9A-Fa-f]{16}$") ? "invalid_or_empty_declaration" :
                    all.Any(x => x.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase) && x.Loaded) ? "loaded" :
                    all.Any(x => x.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase)) ? "available_not_loaded" : "unavailable" })),
            meaning = "Native project-owner snapshot on the editor thread. Root is the on-disk project location; mountedRoot is only a successful native filesystem resolution. Declarations/dependencies/modules are on-disk, not effective inherited compiler settings. No load-order inference or write-access guarantee. Current project need not be the writable addon; select the intended addon explicitly." };
    }
}

[McpServerToolType]
public sealed class ProjectTools(ProjectContext projects)
{
    [McpServerTool(Name = "workbench_project_context", ReadOnly = true, OpenWorld = false)]
    [Description("Read live project, addons and mounted roots; scope=loaded|available|all, limit=1-100. Returns projectToken for pak_prepare. No adapter or Play required.")]
    public async Task<object> Context(int processId, string scope = "loaded", string query = "", int offset = 0, int limit = 32,
        string? projectToken = null, CancellationToken cancellationToken = default)
    {
        try { return await projects.Inspect(processId, scope, query, offset, limit, projectToken, cancellationToken); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }
}
