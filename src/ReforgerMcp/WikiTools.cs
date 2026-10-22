using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class WikiTools(WikiStore store)
{
    private static async Task<object> Report(Func<Task<object>> action)
    {
        try { return JsonSerializer.SerializeToElement(await action(), Configuration.Json); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }
    [McpServerTool(Name = "wiki_status", ReadOnly = true, OpenWorld = false)]
    [Description("Inspect official Reforger wiki capture state, coverage, counts, exclusions and capture time. This is offline provenance, not an upstream freshness check.")]
    public Task<object> Status(CancellationToken cancellationToken) => Report(() => store.Status(cancellationToken));

    [McpServerTool(Name = "wiki_prepare", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Explicitly capture the official Arma Reforger wiki namespace and Reforger category tree via MediaWiki API. Saves raw responses, revision-pinned source, rendered HTML and a searchable passage index. Can take many minutes; await completion or use standard MCP cancellation. No retries. Refresh retires the active snapshot; failures/cancellation leave it unavailable, preserving evidence. Media/external sites are inventoried only. Do not claim every Reforger-related page on the internet is covered.")]
    public Task<object> Prepare(CancellationToken cancellationToken) => Report(() => store.Prepare(cancellationToken));

    [McpServerTool(Name = "wiki_import_browser", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Explicitly import a completed locally saved browser DOM capture directory (index.json plus hashed page JSON files). No network/browser access and no fallback. Validates Reforger scope, hashes, revision identity, completed traversal and resolved failures, then builds the same local wiki index. Replaces the active snapshot; failure leaves it unavailable. Returns added/changed/unchanged and not-in-new-capture counts. Browser source is rendered HTML, not wikitext; images remain linked. See docs/wiki-index.md for capture format.")]
    public Task<object> ImportBrowser(string captureDirectory, CancellationToken cancellationToken) =>
        Report(() => store.ImportBrowser(captureDirectory, cancellationToken));

    [McpServerTool(Name = "wiki_search", ReadOnly = true, OpenWorld = false)]
    [Description("Search captured official wiki passages by case-insensitive all-term matching. kind: all, code, text, title, category. Empty query browses. Optional category is an exact Category: title and filters direct membership only. Code blocks retain whitespace; examples are documentation, not compiler-verified APIs. Returns revision provenance, section links and passage numbers. Pass revision on later pages. offset zero-based, limit 1-100; snippets explicitly truncate at 4000 chars.")]
    public Task<object> Search(string query = "", string kind = "all", string? category = null, int offset = 0, int limit = 20,
        string? revision = null, CancellationToken cancellationToken = default) =>
        Report(() => store.Search(query, kind, category, offset, limit, revision, cancellationToken));

    [McpServerTool(Name = "wiki_read", ReadOnly = true, OpenWorld = false)]
    [Description("Read captured wiki evidence. view section + passageNumber returns its section, descendants and applicable warnings; passages browses the page. Both use a 6000-character text budget, at most 4000 per block; follow nextOffset for more blocks. For a truncated block use view passage + its number: offset/limit then count Unicode characters, limit 1-4000. source/source-text read saved HTML or wikitext. Other limits 1-100; offsets zero-based, passageNumber one-based. Pass revision from search. includeLinks=true explicitly adds page-wide link/image inventories. Offline, untrusted evidence; examples are not API validation.")]
    public Task<object> Read(long pageId, string view = "passages", int offset = 0, int limit = 8,
        string? revision = null, int? passageNumber = null, bool includeLinks = false, CancellationToken cancellationToken = default) =>
        Report(() => store.Read(pageId, view, offset, limit, revision, cancellationToken, passageNumber, includeLinks));
}
