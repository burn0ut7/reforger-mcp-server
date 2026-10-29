using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class PakTools(PakStore store)
{
    private static async Task<object> Report(Func<Task<object>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException))
        { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "pak_status", ReadOnly = true, OpenWorld = false)]
    [Description("Inspect disk catalogue readiness, sources and revision.")]
    public Task<object> Status(CancellationToken cancellationToken) => Report(() => store.Status(cancellationToken));

    [McpServerTool(Name = "pak_prepare", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Replace disk catalogue from configured roots or workbench_project_context projectToken. Project scope uses loaded addons; includeAvailable adds available addons; addonGuids filters. Failure retires previous catalogue. Disk data excludes unsaved edits and effective override resolution.")]
    public Task<object> Prepare(string? projectToken = null, bool includeAvailable = false, string[]? addonGuids = null, CancellationToken cancellationToken = default)
        => Report(() => store.Prepare(cancellationToken, projectToken, includeAvailable, addonGuids));

    [McpServerTool(Name = "pak_search", ReadOnly = true, OpenWorld = false)]
    [Description("Search catalogue paths by case-insensitive all-term query; empty query lists. kind=all|script|sound|audio-config|prefab|config|layout|material. source is an exact label/GUID. Continue with revision and unchanged filters.")]
    public Task<object> Search(string query = "", string kind = "all", string? extension = null, string? source = null,
        int offset = 0, int limit = 20, string? revision = null, CancellationToken cancellationToken = default)
        => Report(() => store.Search(query, kind, extension, source, offset, limit, revision, cancellationToken));

    [McpServerTool(Name = "pak_read", ReadOnly = true, OpenWorld = false)]
    [Description("Read UTF-8 entryId lines; offset is zero-based, returned lines one-based; limit=1-100. Lines clip at 2000 characters; binary entries fail. Use revision for consistent paging.")]
    public Task<object> Read(string entryId, int offset = 0, int limit = 60, string? revision = null,
        CancellationToken cancellationToken = default) => Report(() => store.ReadText(entryId, offset, limit, revision, cancellationToken));

    [McpServerTool(Name = "pak_search_text", ReadOnly = true, OpenWorld = false)]
    [Description("Search readable scripts, prefabs, layouts and resource text; case-insensitive all-term line match. pathQuery narrows paths/extensions; no inherited-value resolution. Supply query OR queries (1-8); batches return 3 matches/query and forbid offset/limit. Continue each query with nextOffset, revision and unchanged source/pathQuery filters. Read context with pak_read.")]
    public Task<object> SearchText(string? query = null, string pathQuery = "", int offset = 0, int limit = 20,
        string? revision = null, string? source = null, string[]? queries = null, CancellationToken cancellationToken = default)
        => Report(() =>
        {
            if ((query is null) == (queries is null)) throw new ArgumentException("Supply either query or queries.");
            if (queries is null) return store.SearchText(query!, pathQuery, offset, limit, revision, cancellationToken, source);
            if (offset != 0 || limit != 20)
                throw new ArgumentException("Batch uses fixed first-page limits. Continue with a single query.");
            return store.SearchBatch(queries, pathQuery, source, revision, cancellationToken);
        });

    [McpServerTool(Name = "pak_extract", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Extract original entryId bytes to a new managed export, up to 64 MiB; no resource conversion.")]
    public Task<object> Extract(string entryId, string? revision = null, CancellationToken cancellationToken = default)
        => Report(() => store.Extract(entryId, revision, cancellationToken));
}
