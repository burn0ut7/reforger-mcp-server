using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

public sealed record WikiResearchPage(WikiPage Page, WikiPassage[] Excerpts, int MatchedTerms, int WarningsOmitted = 0, int? FocusPassage = null);
public sealed record WikiResearchResult(string Revision, DateTimeOffset Captured, string[] Terms, int Total, WikiResearchPage[] Pages, string[]? ExpandedTerms = null);

[McpServerToolType]
public sealed class ModResearch(WikiStore wiki, PakStore paks)
{
    [McpServerTool(Name = "research_mod", ReadOnly = true, OpenWorld = false)]
    [Description("Start a modding question here. Returns 2 ranked wiki pages by default (limit 1-5), short excerpts with applicable warnings, and installed file matches. Follow each page's read call for bounded section context. expandedTerms shows limited terminology bridges. No network, preparation or API verification. Optional source filters installed matches. Use wiki_search for literal/category lookup and pak_search_text for declarations/usages.")]
    public async Task<object> Research(string query, string? source = null, int limit = 2, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await wiki.Research(query, limit, cancellationToken);
            var references = result.Pages.ToDictionary(p => p.Page.PageId,
                p => p.Page.Links.Where(l => l.StartsWith("enfusion://", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.Ordinal).ToArray());
            // Bound page-level links separately from excerpts. Never imply a link came from an excerpt.
            var selected = result.Pages.ToDictionary(p => p.Page.PageId, p =>
            {
                var context = string.Join("\n", p.Excerpts.Select(b => b.Text));
                return references[p.Page.PageId].Select(ParseLink)
                    .OrderByDescending(l => l.Path is null ? -1 : result.Terms.Count(t =>
                        Path.GetFileNameWithoutExtension(l.Path).Contains(t, StringComparison.OrdinalIgnoreCase)))
                    .ThenByDescending(l => l.Path is not null && Regex.IsMatch(context,
                        @"(?<![\w])" + Regex.Escape(Path.GetFileNameWithoutExtension(l.Path)) + @"(?![\w])", RegexOptions.IgnoreCase))
                    .ThenByDescending(l => l.Path is null ? -1 : result.Terms.Count(t => l.Path.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    .Take(2).ToArray();
            });
            var paths = selected.Values.SelectMany(p => p).Where(p => p.Path is not null)
                .Select(p => p.Path!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var installed = await paks.ResolvePaths(paths, source, cancellationToken);
            return JsonSerializer.SerializeToElement(new
            {
                query, result.Terms, result.ExpandedTerms, wikiRevision = result.Revision, captured = result.Captured,
                scope = "Offline wiki guidance and exact installed file-path matches; not verified APIs, resource GUID resolution or live Workbench state.",
                totalMatchingPages = result.Total, omittedPages = Math.Max(0, result.Total - result.Pages.Length),
                pages = result.Pages.Select(p => new
                {
                    p.Page.PageId, p.Page.Title, p.Page.RevisionUrl, p.Page.CaptureMethod, p.Page.CapturedAt,
                    p.MatchedTerms, p.WarningsOmitted,
                    read = new { tool = "wiki_read", arguments = new { pageId = p.Page.PageId, view = "section",
                        passageNumber = p.FocusPassage, revision = result.Revision, limit = 8 } },
                    excerpts = p.Excerpts.Select(b => new
                    {
                        b.Kind, b.SectionPath, b.Context, passageNumber = b.Number,
                        text = string.Concat(b.Text.EnumerateRunes().Take(400)),
                        truncated = b.Text.EnumerateRunes().Count() > 400
                    }),
                    fileLinks = selected[p.Page.PageId],
                    omittedFileLinks = Math.Max(0, references[p.Page.PageId].Length - 2)
                }),
                installed
            }, Configuration.Json);
        }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }

    public sealed record FileLink(string Url, string? Path, int? HistoricalLine, string State, string? Addon = null);
    public static FileLink ParseLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "enfusion" ||
            uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return new(url, null, null, "unsupported");
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        int? line = null;
        string? addon = null;
        if (uri.Host == "scripteditor")
        {
            var parts = path.Split(';');
            if (parts.Length > 2 || (parts.Length == 2 && (!int.TryParse(parts[1], out var n) || n < 1)))
                return new(url, null, null, "unsupported");
            if (parts.Length == 2) line = int.Parse(parts[1]);
            path = parts[0];
            if (!path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".c", StringComparison.OrdinalIgnoreCase))
                return new(url, null, null, "unsupported");
        }
        else if (uri.Host == "resourcemanager")
        {
            var separator = path.IndexOf(':');
            if (!path.StartsWith('~') || separator <= 1) return new(url, null, null, "unsupported");
            addon = path[1..separator];
            if (string.IsNullOrWhiteSpace(addon) || addon.Any(c => char.IsControl(c) || c is '/' or '\\' or '|' or ';'))
                return new(url, null, null, "unsupported");
            path = path[(separator + 1)..];
        }
        else return new(url, null, null, "unsupported");
        if (path.Length == 0 || path.Contains('\\') || path.Contains('|') || path.Contains(':') ||
            path.Split('/').Any(p => p is "" or "." or "..") || path.Any(char.IsControl))
            return new(url, null, null, "unsupported");
        return new(url, path, line, "path-only; addon identity and linked line number are not verified", addon);
    }
}
