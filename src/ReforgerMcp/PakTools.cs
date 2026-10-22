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
    [Description("Check the single packed/unpacked source catalogue, per-source coverage, project binding, script mirror and exclusions. Does not prepare automatically. Project-bound catalogues reobserve Workbench. Loose scripts/project files are rehashed; source sets, sizes and timestamps are checked.")]
    public Task<object> Status(CancellationToken cancellationToken) => Report(() => store.Status(cancellationToken));

    [McpServerTool(Name = "pak_prepare", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Explicitly prepare the single source catalogue from configured pakDirectories/unpackedDirectories, OR supply projectToken from workbench_project_context to catalogue both PAKs and loose files in observed addon roots. Project mode defaults to loaded addons; includeAvailable adds available addons, and addonGuids optionally selects exact GUIDs within that scope. Project mode replaces configured source selection, labels sources by GUID and requires the same live context on subsequent queries. Hashes inputs and mirrors .c scripts; never writes addon files. Disk content only, not unsaved buffers or effective override precedence. Each refresh replaces the active catalogue; preparation failure retires it. May take minutes; standard MCP cancellation supported.")]
    public Task<object> Prepare(string? projectToken = null, bool includeAvailable = false, string[]? addonGuids = null, CancellationToken cancellationToken = default)
        => Report(() => store.Prepare(cancellationToken, projectToken, includeAvailable, addonGuids));

    [McpServerTool(Name = "pak_search", ReadOnly = true, OpenWorld = false)]
    [Description("Find packed or loose assets/scripts by case-insensitive path terms (all terms required). Empty query lists entries. kind: all, script, sound, audio-config, prefab, config, layout, material. Optional extension/source filters; source is a label or observed addon GUID from pak_status. Storage distinguishes pak/loose; duplicate paths stay distinct with no inferred winner. Returns exact entry IDs and provenance. Pass revision on later pages.")]
    public Task<object> Search(string query = "", string kind = "all", string? extension = null, string? source = null,
        int offset = 0, int limit = 20, string? revision = null, CancellationToken cancellationToken = default)
        => Report(() => store.Search(query, kind, extension, source, offset, limit, revision, cancellationToken));

    [McpServerTool(Name = "pak_read", ReadOnly = true, OpenWorld = false)]
    [Description("Read an exact packed or loose entryId as strict UTF-8 text with one-based line numbers and byte hash. Supports source scripts and readable configs/prefabs/layouts. Binary or invalid UTF-8 fails explicitly. offset is zero-based, limit 1-100. Lines over 2000 characters are marked truncated; use pak_extract for full original bytes. Source proto external declarations do not include native implementation. Pass revision on later pages.")]
    public Task<object> Read(string entryId, int offset = 0, int limit = 60, string? revision = null,
        CancellationToken cancellationToken = default) => Report(() => store.ReadText(entryId, offset, limit, revision, cancellationToken));

    [McpServerTool(Name = "pak_search_text", ReadOnly = true, OpenWorld = false)]
    [Description("Search readable mirrored .c scripts for declarations, examples and usages. Supply query for one search, or queries (1–8 strings) for several searches in one scan, with up to 3 short matches each. Whitespace-separated terms within each query must all occur on one line, ignoring case. source is an exact source label or addon GUID from pak_status; pathQuery filters entry IDs by the same term rule. Both filters apply to the whole batch. Read context with pak_read(entryId, offset: line-1, revision). Continue a group with query, offset: nextOffset, revision and the same filters. Batch mode uses fixed first-page limits; omit offset/limit. Returns provenance and unreadable coverage; verifies script hashes. Literal text search, not semantic resolution.")]
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
    [Description("Export one exact packed or loose entryId to a fresh managed export directory and return its file path plus provenance. Never overwrites existing exports or writes to the game. Supports raw and zlib payloads up to 64 MiB stored/decoded. Unsupported compression fails. WAV/SND/models/textures remain original bytes; no conversion or playback guarantee. pak_prepare already exports all .c files.")]
    public Task<object> Extract(string entryId, string? revision = null, CancellationToken cancellationToken = default)
        => Report(() => store.Extract(entryId, revision, cancellationToken));
}
