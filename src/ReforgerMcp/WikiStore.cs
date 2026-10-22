using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ReforgerMcp;

public sealed record WikiPassage(int Number, string Kind, string Section, string Anchor, string Text,
    string SectionPath = "", string? Context = null, int HeadingLevel = 0);
public sealed record WikiPage(long PageId, string Title, int Namespace, long RevisionId, string Timestamp,
    string Url, string RevisionUrl, string SourceSha256, string[] Categories, string[] Links,
    string[] Images, string[] ExternalLinks, string[] Templates, string? Redirect, WikiPassage[] Passages,
    string SourceFormat = "wikitext", string CaptureMethod = "mediawiki-api", DateTimeOffset? CapturedAt = null,
    string? ContentSha256 = null);
public sealed record WikiSnapshot(int Schema, string Revision, DateTimeOffset Started, DateTimeOffset Completed,
    string Api, string Scope, WikiPage[] Pages, string[] Excluded, string[] Missing,
    string CaptureMethod = "mediawiki-api");
public sealed record WikiManifest(string State, string Revision, string? Sha256, string? Error);

/// <summary>Owns official MediaWiki acquisition, retained evidence and the offline passage index.</summary>
public sealed partial class WikiStore : IDisposable
{
    public const string Api = "https://community.bistudio.com/wikidata/api.php";
    public const string Scope = "All pages in namespace 5050 (Arma Reforger), plus namespace 0 articles and Reforger categories reached from Category:Arma Reforger. Other namespaces and unrelated categories are excluded. Links/media are inventoried, not recursively downloaded.";
    private readonly string root;
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int responseNumber;
    public WikiStore(Configuration config) : this(config, new HttpClientHandler { AllowAutoRedirect = false }) { }
    // Handler injection exercises the same owning implementation against deterministic protocol fixtures.
    public WikiStore(Configuration config, HttpMessageHandler handler)
    {
        root = Path.Combine(config.DataDirectory, "wiki");
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90), MaxResponseContentBufferSize = 32 * 1024 * 1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ReforgerMcp/1.0 (official-wiki-index; personal-modding-research)");
    }
    public void Dispose() { http.Dispose(); gate.Dispose(); }
    private string ManifestPath => Path.Combine(root, "current.json");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static async Task Save(string path, object value, CancellationToken ct) =>
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Configuration.Json), ct);
    private async Task Manifest(WikiManifest value, CancellationToken ct)
    {
        var temp = ManifestPath + ".tmp";
        await Save(temp, value, ct);
        File.Move(temp, ManifestPath, true);
    }
    private WikiManifest? Current() => File.Exists(ManifestPath)
        ? JsonSerializer.Deserialize<WikiManifest>(File.ReadAllText(ManifestPath), Configuration.Json) : null;
    private async Task<T> Locked<T>(Func<Task<T>> action, CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) throw new InvalidOperationException("Wiki store busy; await the active operation.");
        try { return await action(); } finally { gate.Release(); }
    }
    public Task<object> Status(CancellationToken ct) => Locked<object>(async () =>
    {
        var manifest = Current();
        if (manifest?.State != "ready") return new { state = manifest?.State is "capturing" or "importing" ? "interrupted" : manifest?.State ?? "unprepared", manifest, scope = Scope };
        var snapshot = await Load(null, ct);
        return new { state = "ready", snapshot.Revision, snapshot.Started, snapshot.Completed,
            pages = snapshot.Pages.Length, passages = snapshot.Pages.Sum(p => p.Passages.Length),
            codeExamples = snapshot.Pages.Sum(p => p.Passages.Count(x => x.Kind == "code")),
            categories = snapshot.Pages.Count(p => p.Namespace == 14), snapshot.Scope, snapshot.CaptureMethod,
            excluded = snapshot.Excluded, missing = snapshot.Missing, directory = Path.Combine(root, snapshot.Revision),
            freshness = "Offline capture; current upstream revisions have not been checked. Explicitly prepare to refresh." };
    }, ct);

    /// <summary>Explicit offline import of operator-captured browser DOM; never invoked as a network fallback.</summary>
    public Task<object> ImportBrowser(string captureDirectory, CancellationToken ct) => Locked<object>(async () =>
    {
        captureDirectory = Path.GetFullPath(captureDirectory);
        if (!Directory.Exists(captureDirectory)) throw new ArgumentException("Browser capture directory does not exist.");
        Directory.CreateDirectory(root);
        var revision = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(root, revision);
        Directory.CreateDirectory(directory);
        WikiSnapshot? previous = Current()?.State == "ready" ? await Load(null, ct, allowLegacy: true) : null;
        await Manifest(new("importing", revision, null, null), ct);
        try
        {
            var manifestBytes = await File.ReadAllBytesAsync(Path.Combine(captureDirectory, "index.json"), ct);
            using var capture = JsonDocument.Parse(manifestBytes);
            var index = capture.RootElement;
            if (index.GetProperty("schema").GetInt32() != 1 ||
                index.GetProperty("queue").GetArrayLength() != 0 ||
                !index.TryGetProperty("completedAt", out var completed))
                throw new InvalidDataException("Browser capture is incomplete or has an unsupported schema.");
            var entries = index.GetProperty("pages").EnumerateArray().ToArray();
            if (entries.Length == 0) throw new InvalidDataException("Browser capture contains no pages.");
            await File.WriteAllBytesAsync(Path.Combine(directory, "browser-capture.json"), manifestBytes, ct);
            var pages = new Dictionary<long, WikiPage>();
            var aliases = new List<object>();
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                var file = entry.GetProperty("file").GetString()!;
                if (!Regex.IsMatch(file, @"\A[a-f0-9]{20}\.json\z")) throw new InvalidDataException("Invalid browser capture filename.");
                var capturePath = Path.Combine(captureDirectory, file);
                if ((File.GetAttributes(capturePath) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked browser capture files are unsupported.");
                using var pageDoc = JsonDocument.Parse(await File.ReadAllBytesAsync(capturePath, ct));
                var page = pageDoc.RootElement;
                var url = page.GetProperty("url").GetString()!;
                var uri = new Uri(url);
                var title = Uri.UnescapeDataString(uri.AbsolutePath["/wiki/".Length..]).Replace('_', ' ');
                if (uri.Scheme != "https" || uri.Host != "community.bistudio.com" || !uri.IsDefaultPort ||
                    !uri.AbsolutePath.StartsWith("/wiki/", StringComparison.Ordinal) || uri.Query.Length != 0 ||
                    !(title.StartsWith("Arma Reforger:", StringComparison.Ordinal) ||
                      title == "Category:Arma Reforger/Modding" || title.StartsWith("Category:Arma Reforger/Modding/", StringComparison.Ordinal)))
                    throw new InvalidDataException("Browser capture outside Reforger modding scope: " + url);
                var html = page.GetProperty("html").GetString()!;
                if (!html.TrimEnd().EndsWith("</html>", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Incomplete rendered HTML (capture large DOM strings in chunks): " + file);
                var bytes = Encoding.UTF8.GetBytes(html);
                var hash = Hash(bytes);
                if (hash != page.GetProperty("htmlSha256").GetString() || hash != entry.GetProperty("htmlSha256").GetString())
                    throw new InvalidDataException("Browser HTML hash mismatch: " + file);
                var dom = new HtmlParser().ParseDocument(html);
                var content = dom.QuerySelector("#mw-content-text") ?? throw new InvalidDataException("Missing wiki article content: " + file);
                // Read identifiers from the captured MediaWiki configuration text, never execute it.
                long Id(string key)
                {
                    var match = Regex.Match(html, "\"" + key + "\"\\s*:\\s*(\\d+)");
                    return match.Success && long.TryParse(match.Groups[1].Value, out var id) && id > 0
                        ? id : throw new InvalidDataException("Missing captured MediaWiki " + key + ": " + file);
                }
                var pageId = Id("wgArticleId");
                var revisionId = Id("wgRevisionId");
                var ns = (int)Id("wgNamespaceNumber");
                if (ns != (title.StartsWith("Category:", StringComparison.Ordinal) ? 14 : 5050))
                    throw new InvalidDataException("Captured namespace does not match the page URL.");
                var permalink = dom.QuerySelector("#t-permalink a")?.GetAttribute("href");
                if (permalink is null || !Regex.IsMatch(permalink, @"(?:\?|&)oldid=" + revisionId + @"(?:&|$)"))
                    throw new InvalidDataException("Missing or mismatched captured revision link: " + file);
                var capturedAt = page.GetProperty("capturedAt").GetDateTimeOffset();
                var passages = Extract(content.OuterHtml);
                var contentHash = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(passages, Configuration.Json)));
                var links = content.QuerySelectorAll("a[href]").Select(a => new Uri(uri, a.GetAttribute("href")!).AbsoluteUri).Distinct().ToArray();
                var categories = dom.QuerySelectorAll("#mw-normal-catlinks a[title]").Select(a => a.GetAttribute("title")!)
                    .Where(t => t.StartsWith("Category:", StringComparison.Ordinal)).Distinct().ToArray();
                var images = content.QuerySelectorAll("img[src]").Select(e => new Uri(uri, e.GetAttribute("src")!).AbsoluteUri).Distinct().ToArray();
                var imported = new WikiPage(pageId, title, ns, revisionId,
                    dom.QuerySelector("#footer-info-lastmod")?.TextContent.Trim() ?? "Revision timestamp unavailable; see capturedAt.",
                    url, new Uri(uri, permalink).AbsoluteUri, hash, categories, links, images,
                    links.Where(l => new Uri(l).Scheme is "http" or "https" && new Uri(l).Host != uri.Host).ToArray(), [], null, passages,
                    "rendered-html", "browser-dom", capturedAt, contentHash);
                if (pages.TryGetValue(pageId, out var existing))
                {
                    if (existing.RevisionId != revisionId || existing.ContentSha256 != contentHash)
                    {
                        await Save(Path.Combine(directory, "conflict-" + pageId + ".json"), new { existing, imported }, ct);
                        throw new InvalidDataException("Conflicting captures of page " + pageId + "; reconcile explicitly.");
                    }
                    aliases.Add(new { url, pageId, file, indexedUrl = existing.Url });
                    continue;
                }
                pages.Add(pageId, imported);
                await File.WriteAllBytesAsync(Path.Combine(directory, pageId + ".html"), bytes, ct);
            }
            // A redirect-only interruption is resolved only if its destination really exists in this capture.
            foreach (var failure in index.GetProperty("failures").EnumerateArray())
                if (!failure.TryGetProperty("resolvedUrl", out var resolved) ||
                    !pages.Values.Any(p => p.Url == resolved.GetString()))
                    throw new InvalidDataException("Browser capture has unresolved failures.");
            var snapshot = new WikiSnapshot(2, revision, index.GetProperty("startedAt").GetDateTimeOffset(),
                completed.GetDateTimeOffset(), "", index.GetProperty("scope").GetString()!, pages.Values.OrderBy(p => p.PageId).ToArray(),
                [], [], "browser-dom");
            var path = Path.Combine(directory, "index.json");
            await Save(path, snapshot, ct);
            await Save(Path.Combine(directory, "aliases.json"), aliases, ct);
            var old = previous?.Pages.ToDictionary(p => p.PageId) ?? [];
            var added = pages.Keys.Count(id => !old.ContainsKey(id));
            var changed = pages.Values.Count(p => old.TryGetValue(p.PageId, out var prior) &&
                (p.RevisionId != prior.RevisionId || p.ContentSha256 != prior.ContentSha256 || p.SourceFormat != prior.SourceFormat));
            await Manifest(new("ready", revision, Hash(await File.ReadAllBytesAsync(path, ct)), null), ct);
            return new { state = "ready", revision, captureMethod = "browser-dom", pages = pages.Count, aliases = aliases.Count,
                passages = pages.Values.Sum(p => p.Passages.Length), codeExamples = pages.Values.Sum(p => p.Passages.Count(b => b.Kind == "code")),
                added, changed, unchanged = pages.Count - added - changed, notInNewCapture = old.Keys.Count(id => !pages.ContainsKey(id)),
                directory, scope = snapshot.Scope,
                limitations = "Rendered DOM, not original wikitext. Images/external downloads are linked, not bundled. Templates are rendered at capture time. No upstream freshness check or automatic browser access." };
        }
        catch (Exception ex)
        {
            await Manifest(new(ex is OperationCanceledException ? "cancelled" : "failed", revision, null, ex.Message), CancellationToken.None);
            throw;
        }
    }, ct);

    private async Task<JsonElement> Request(Dictionary<string, string> parameters, string evidence, CancellationToken ct)
    {
        parameters = new(parameters) { ["format"] = "json", ["formatversion"] = "2", ["maxlag"] = "5" };
        var url = Api + "?" + string.Join("&", parameters.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));
        // One serial request stream, no automatic retries or alternate scraping backend.
        await Task.Delay(150, ct);
        using var response = await http.GetAsync(url, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var stem = Path.Combine(evidence, (++responseNumber).ToString("D6"));
        await File.WriteAllBytesAsync(stem + ".json", bytes, ct);
        await Save(stem + ".request.json", new { url, fetched = DateTimeOffset.UtcNow, status = (int)response.StatusCode, sha256 = Hash(bytes) }, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Official Wiki HTTP {(int)response.StatusCode}; response retained at {stem}.json. No retry was made.");
        using var doc = JsonDocument.Parse(bytes);
        var result = doc.RootElement.Clone();
        if (result.TryGetProperty("error", out var error)) throw new InvalidDataException("Official Wiki API error: " + error);
        if (result.TryGetProperty("warnings", out var warnings)) throw new InvalidDataException("Official Wiki API warning requires inspection: " + warnings);
        return result;
    }
    private async Task<List<JsonElement>> List(Dictionary<string, string> request, string name, string evidence, CancellationToken ct)
    {
        var items = new List<JsonElement>();
        var seen = new HashSet<string>();
        while (true)
        {
            var result = await Request(request, evidence, ct);
            items.AddRange(result.GetProperty("query").GetProperty(name).EnumerateArray().Select(x => x.Clone()));
            if (!result.TryGetProperty("continue", out var next)) break;
            if (!seen.Add(next.GetRawText())) throw new InvalidDataException("Repeated wiki continuation token.");
            foreach (var p in next.EnumerateObject()) request[p.Name] = p.Value.ToString();
        }
        return items;
    }
    public Task<object> Prepare(CancellationToken ct) => Locked<object>(async () =>
    {
        Directory.CreateDirectory(root);
        var revision = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(root, revision);
        var evidence = Path.Combine(directory, "responses");
        Directory.CreateDirectory(evidence);
        var started = DateTimeOffset.UtcNow;
        await Manifest(new("capturing", revision, null, null), ct);
        try
        {
            await Request(new() { ["action"] = "query", ["meta"] = "siteinfo", ["siprop"] = "general|namespaces|rightsinfo" }, evidence, ct);
            var discovered = new Dictionary<long, (string Title, int Namespace)>();
            var excluded = new SortedSet<string>(StringComparer.Ordinal);
            var missing = new List<string>();
            var categories = new Queue<string>(["Category:Arma Reforger"]);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in await List(new() { ["action"] = "query", ["list"] = "allpages", ["apnamespace"] = "5050", ["aplimit"] = "500" }, "allpages", evidence, ct))
                discovered[item.GetProperty("pageid").GetInt64()] = (item.GetProperty("title").GetString()!, 5050);
            while (categories.TryDequeue(out var category))
            {
                if (!visited.Add(category)) continue;
                var info = await Request(new() { ["action"] = "query", ["titles"] = category }, evidence, ct);
                foreach (var p in info.GetProperty("query").GetProperty("pages").EnumerateArray())
                    if (!p.TryGetProperty("missing", out _)) discovered[p.GetProperty("pageid").GetInt64()] = (p.GetProperty("title").GetString()!, 14);
                    else missing.Add(category);
                foreach (var item in await List(new() { ["action"] = "query", ["list"] = "categorymembers", ["cmtitle"] = category, ["cmlimit"] = "500" }, "categorymembers", evidence, ct))
                {
                    var title = item.GetProperty("title").GetString()!;
                    var ns = item.GetProperty("ns").GetInt32();
                    if (ns == 14 && (title == "Category:Arma Reforger" || title.StartsWith("Category:Arma Reforger/", StringComparison.Ordinal)))
                        categories.Enqueue(title);
                    else if (ns is 0 or 5050) discovered[item.GetProperty("pageid").GetInt64()] = (title, ns);
                    else excluded.Add(title);
                }
            }
            if (discovered.Count == 0 || missing.Count != 0) throw new InvalidDataException("Wiki discovery empty or root category missing: " + string.Join(", ", missing));
            var pages = new List<WikiPage>();
            foreach (var id in discovered.Keys.Order())
            {
                ct.ThrowIfCancellationRequested();
                var sourceResponse = await Request(new() { ["action"] = "query", ["pageids"] = id.ToString(),
                    ["prop"] = "revisions", ["rvprop"] = "ids|timestamp|content", ["rvslots"] = "main" }, evidence, ct);
                var source = sourceResponse.GetProperty("query").GetProperty("pages")[0];
                if (source.TryGetProperty("missing", out _)) throw new InvalidDataException($"Page {id} disappeared during capture; refresh explicitly.");
                var rev = source.GetProperty("revisions")[0];
                var revisionId = rev.GetProperty("revid").GetInt64();
                var text = rev.GetProperty("slots").GetProperty("main").GetProperty("content").GetString()!;
                await File.WriteAllTextAsync(Path.Combine(directory, id + ".wiki"), text, ct);
                var parsedResponse = await Request(new() { ["action"] = "parse", ["oldid"] = revisionId.ToString(),
                    ["prop"] = "text|sections|links|categories|images|externallinks|templates|revid" }, evidence, ct);
                var parsed = parsedResponse.GetProperty("parse");
                if (parsed.GetProperty("revid").GetInt64() != revisionId || parsed.GetProperty("pageid").GetInt64() != id)
                    throw new InvalidDataException("Wiki parse/source revision identity mismatch.");
                var html = parsed.GetProperty("text").GetString()!;
                await File.WriteAllTextAsync(Path.Combine(directory, id + ".html"), html, ct);
                var title = source.GetProperty("title").GetString()!;
                var redirect = Regex.Match(text, @"^\s*#redirect\s*\[\[([^\]]+)\]\]", RegexOptions.IgnoreCase);
                pages.Add(new(id, title, source.GetProperty("ns").GetInt32(), revisionId, rev.GetProperty("timestamp").GetString()!,
                    "https://community.bistudio.com/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_')),
                    "https://community.bistudio.com/wiki?oldid=" + revisionId, Hash(Encoding.UTF8.GetBytes(text)),
                    parsed.GetProperty("categories").EnumerateArray().Select(x => "Category:" + x.GetProperty("category").GetString()!.Replace('_', ' ')).ToArray(),
                    parsed.GetProperty("links").EnumerateArray().Select(x => x.GetProperty("title").GetString()!).ToArray(),
                    parsed.GetProperty("images").EnumerateArray().Select(x => x.GetString()!).ToArray(),
                    parsed.GetProperty("externallinks").EnumerateArray().Select(x => x.GetString()!).ToArray(),
                    parsed.GetProperty("templates").EnumerateArray().Select(x => x.GetProperty("title").GetString()!).ToArray(),
                    redirect.Success ? redirect.Groups[1].Value : null, Extract(html)));
                await Save(Path.Combine(directory, "progress.json"), new { captured = pages.Count, discovered = discovered.Count, lastTitle = title }, ct);
            }
            var snapshot = new WikiSnapshot(2, revision, started, DateTimeOffset.UtcNow, Api, Scope, pages.ToArray(), excluded.ToArray(), missing.ToArray());
            var path = Path.Combine(directory, "index.json");
            await Save(path, snapshot, ct);
            await Manifest(new("ready", revision, Hash(await File.ReadAllBytesAsync(path, ct)), null), ct);
            return new { state = "ready", revision, pages = pages.Count, passages = pages.Sum(p => p.Passages.Length),
                codeExamples = pages.Sum(p => p.Passages.Count(x => x.Kind == "code")), directory, scope = Scope };
        }
        catch (Exception ex)
        {
            await Manifest(new(ex is OperationCanceledException ? "cancelled" : "failed", revision, null, ex.Message), CancellationToken.None);
            throw;
        }
    }, ct);

    private async Task<WikiSnapshot> Load(string? revision, CancellationToken ct, bool allowLegacy = false)
    {
        var m = Current();
        if (m?.State != "ready") throw new InvalidOperationException("Wiki index unavailable; run wiki_prepare explicitly. State: " + (m?.State ?? "unprepared"));
        if (!Guid.TryParseExact(m.Revision, "N", out _)) throw new InvalidDataException("Invalid wiki manifest revision.");
        if (revision is not null && revision != m.Revision) throw new InvalidOperationException("Wiki revision changed; restart paging.");
        var bytes = await File.ReadAllBytesAsync(Path.Combine(root, m.Revision, "index.json"), ct);
        if (Hash(bytes) != m.Sha256) throw new InvalidDataException("Wiki index hash mismatch; refresh explicitly.");
        var snapshot = JsonSerializer.Deserialize<WikiSnapshot>(bytes, Configuration.Json)!;
        if ((snapshot.Schema != 2 && !(allowLegacy && snapshot.Schema == 1)) || snapshot.Revision != m.Revision)
            throw new InvalidDataException("Wiki extraction format is incompatible; explicitly reimport the saved browser capture or prepare a new API capture.");
        return snapshot;
    }
    private static void Bounds(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new ArgumentException("offset must be >= 0; limit must be 1-100.");
    }
    private static bool Matches(string text, string query) => query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    private static object Clip(string text) => new { text = text.Length > 4000 ? text[..4000] : text, truncated = text.Length > 4000, totalCharacters = text.Length };

    public Task<object> Search(string query, string kind, string? category, int offset, int limit, string? revision, CancellationToken ct) => Locked<object>(async () =>
    {
        Bounds(offset, limit);
        if (kind is not ("all" or "code" or "text" or "title" or "category")) throw new ArgumentException("kind: all, code, text, title, category.");
        var s = await Load(revision, ct);
        var matches = s.Pages.Where(p => category is null || p.Categories.Contains(category, StringComparer.OrdinalIgnoreCase))
            .SelectMany(p => kind is "title" or "category"
                ? (kind != "category" || p.Namespace == 14) && Matches(p.Title, query)
                    ? new[] { (Page: p, Passage: new WikiPassage(0, kind, "", "", p.Title)) } : []
                : p.Passages.Where(b => (kind == "all" || b.Kind == kind || (kind == "text" && b.Kind is "warning" or "note")) && Matches(b.Text, query)).Select(b => (Page: p, Passage: b)))
            .OrderBy(x => x.Page.Title, StringComparer.Ordinal).ThenBy(x => x.Passage.Number).ToArray();
        return new { s.Revision, s.Completed, freshness = "offline capture; upstream unchecked", total = matches.Length,
            nextOffset = offset + Math.Min(limit, Math.Max(0, matches.Length - offset)) < matches.Length ? (int?)(offset + limit) : null,
            results = matches.Skip(offset).Take(limit).Select(x => new { x.Page.PageId, x.Page.Title, x.Page.RevisionId, x.Page.RevisionUrl,
                x.Page.Categories, x.Page.Redirect, x.Page.CaptureMethod, x.Page.SourceFormat, x.Page.CapturedAt,
                x.Passage.Number, x.Passage.Kind, x.Passage.Section, x.Passage.SectionPath, x.Passage.Context,
                url = x.Page.Url + (x.Passage.Anchor.Length == 0 ? "" : "#" + Uri.EscapeDataString(x.Passage.Anchor)), content = Clip(x.Passage.Text) }) };
    }, ct);
    private static (int Start, int End) SectionBounds(WikiPage page, int focus)
    {
        int start = focus;
        while (start > 0 && page.Passages[start].Kind != "heading") start--;
        int level = page.Passages[start].Kind == "heading" ? page.Passages[start].HeadingLevel : 0;
        int end = start + 1;
        while (end < page.Passages.Length && (page.Passages[end].Kind != "heading" || page.Passages[end].HeadingLevel > level)) end++;
        return (start, end);
    }
    private static WikiPassage[] ApplicableWarnings(WikiPage page, int focus) => page.Passages
        .Select((b, i) => (Block: b, Index: i)).Where(x =>
        {
            if (x.Block.Kind != "warning") return false;
            var bounds = SectionBounds(page, x.Index);
            return (bounds.Start == 0 && page.Passages[0].Kind != "heading") ||
                (focus >= bounds.Start && focus < bounds.End);
        }).Select(x => x.Block).ToArray();

    public Task<object> Read(long pageId, string view, int offset, int limit, string? revision, CancellationToken ct,
        int? passageNumber = null, bool includeLinks = false) => Locked<object>(async () =>
    {
        if (view is "passage" or "source-text")
        {
            if (offset < 0 || limit is < 1 or > 4000) throw new ArgumentException("Character views require offset >= 0 and limit 1-4000 (Unicode scalar values).");
        }
        else Bounds(offset, limit);
        var s = await Load(revision, ct);
        var p = s.Pages.SingleOrDefault(p => p.PageId == pageId) ?? throw new ArgumentException("Unknown captured pageId.");
        var directory = Path.Combine(root, s.Revision);
        object CharacterPage(string text, WikiPassage? passage)
        {
            var characters = text.EnumerateRunes().ToArray();
            var selected = new StringBuilder();
            foreach (var rune in characters.Skip(offset).Take(limit)) selected.Append(rune.ToString());
            return new { s.Revision, s.Completed, p.PageId, p.Title, p.RevisionId, p.RevisionUrl, p.Timestamp, p.SourceSha256,
                p.SourceFormat, p.CaptureMethod, p.CapturedAt,
                passageNumber = passage?.Number, kind = passage?.Kind, section = passage?.Section,
                sectionPath = passage?.SectionPath, context = passage?.Context, anchor = passage?.Anchor,
                offsetUnit = "Unicode scalar values", total = characters.Length,
                nextOffset = offset + (long)limit < characters.Length ? (int?)(offset + limit) : null, text = selected.ToString() };
        }
        if (view == "passage")
        {
            var passage = p.Passages.SingleOrDefault(b => b.Number == passageNumber)
                ?? throw new ArgumentException("view passage requires a captured one-based passageNumber.");
            return CharacterPage(passage.Text, passage);
        }
        if (view is "source" or "source-text")
        {
            var sourcePath = Path.Combine(directory, pageId + (p.SourceFormat == "rendered-html" ? ".html" : ".wiki"));
            var bytes = await File.ReadAllBytesAsync(sourcePath, ct);
            if (Hash(bytes) != p.SourceSha256) throw new InvalidDataException("Wiki source hash mismatch.");
            if (view == "source-text") return CharacterPage(Encoding.UTF8.GetString(bytes), null);
            var lines = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n").Split('\n');
            return new { s.Revision, s.Completed, p.PageId, p.Title, p.RevisionId, p.RevisionUrl, p.Timestamp, p.SourceSha256,
                p.SourceFormat, p.CaptureMethod, p.CapturedAt,
                total = lines.Length, nextOffset = (long)offset + limit < lines.Length ? (int?)(offset + limit) : null,
                lines = lines.Skip(offset).Take(limit).Select((line, i) => new { number = offset + i + 1, content = Clip(line) }),
                fullSourcePath = sourcePath };
        }
        if (view is not ("passages" or "section")) throw new ArgumentException("view: passages, section, passage, source or source-text.");
        WikiPassage[] collection = p.Passages;
        if (view == "section")
        {
            int focus = Array.FindIndex(p.Passages, b => b.Number == passageNumber);
            if (focus < 0) throw new ArgumentException("view section requires a captured one-based passageNumber.");
            var (start, end) = SectionBounds(p, focus);
            var prerequisites = ApplicableWarnings(p, focus).Where(b => b.Number < p.Passages[start].Number);
            collection = prerequisites.Concat(p.Passages.Skip(start).Take(end - start)).ToArray();
        }
        const int textBudget = 6000;
        int remaining = textBudget, consumed = 0;
        var previews = new List<object>();
        foreach (var b in collection.Skip(offset).Take(limit))
        {
            if (remaining == 0) break;
            var runes = b.Text.EnumerateRunes().ToArray();
            int take = Math.Min(remaining, Math.Min(4000, runes.Length));
            previews.Add(new { b.Number, b.Kind, b.SectionPath, b.Context, b.Anchor,
                content = new { text = string.Concat(runes.Take(take)), truncated = take < runes.Length, totalCharacters = runes.Length } });
            remaining -= take;
            consumed++;
        }
        return new { s.Revision, s.Completed, p.PageId, p.Title, p.Namespace, p.RevisionId, p.Timestamp, p.Url, p.RevisionUrl,
            p.SourceSha256, p.Categories, links = includeLinks ? p.Links : null, images = includeLinks ? p.Images : null,
            externalLinks = includeLinks ? p.ExternalLinks : null, templates = includeLinks ? p.Templates : null,
            p.Redirect, linksAvailable = p.Links.Length,
            p.SourceFormat, p.CaptureMethod, p.CapturedAt,
            templateMetadataAvailable = p.CaptureMethod == "mediawiki-api",
            view, passageNumber, textBudget, total = collection.Length,
            nextOffset = (long)offset + consumed < collection.Length ? (int?)(offset + consumed) : null,
            passages = previews };
    }, ct);
}
