using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ReforgerMcp;

public sealed partial class WikiStore
{
    public static WikiPassage[] Extract(string html)
    {
        var doc = new HtmlParser().ParseDocument(html);
        foreach (var noise in doc.QuerySelectorAll("script,style,.mw-editsection,.toc,[class*='highlighter-clipboard-button']"))
            noise.Remove();
        var blocks = new List<WikiPassage>();
        var headings = new List<(int Level, string Title)>();
        string section = "", anchor = "";
        string? note = null, context = null;
        string Plain(INode n)
        {
            if (n is IText t) return t.Data;
            if (n is IElement e)
            {
                if (e.LocalName == "img") return e.GetAttribute("alt") ?? "";
                if (e.LocalName == "br") return "\n";
                if (e.TextContent.Trim() is "⚠" or "ⓘ") return "";
                if (e.LocalName is "div" or "p" or "li" or "dt" or "dd")
                    return " " + string.Concat(n.ChildNodes.Select(Plain)) + " ";
            }
            return string.Concat(n.ChildNodes.Select(Plain));
        }
        string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();
        void Emit(string kind, string text)
        {
            if (kind != "code") text = Normalize(text);
            if (text.Length == 0 || text is "⚠" or "ⓘ" || text.StartsWith("Retrieved from ", StringComparison.Ordinal)) return;
            blocks.Add(new(blocks.Count + 1, kind == "text" && note is not null ? note : kind,
                section, anchor, text, string.Join(" > ", headings.Select(h => h.Title)),
                kind == "code" && note is not null ? string.Join(" / ", new[] { context, note }.Where(s => s is not null)) : context,
                kind == "heading" ? headings[^1].Level : 0));
        }
        bool HasCode(IElement e) => e.QuerySelector("pre,[class*='highlighter-scroller']") is not null;
        void Table(IElement table)
        {
            var rows = table.QuerySelectorAll("tr").Where(r => r.Closest("table") == table).ToArray();
            var grid = new Dictionary<(int Row, int Col), IElement>();
            var headers = new Dictionary<int, List<string>>();
            var visited = new HashSet<IElement>();
            bool body = false;
            for (int r = 0; r < rows.Length; r++)
            {
                var cells = rows[r].Children.Where(e => e.LocalName is "td" or "th").ToArray();
                int col = 0;
                foreach (var cell in cells)
                {
                    while (grid.ContainsKey((r, col))) col++;
                    int Span(string attr) => int.TryParse(cell.GetAttribute(attr), out var n) ? Math.Max(1, n) : 1;
                    int height = cell.GetAttribute("rowspan") == "0" ? rows.Length - r : Span("rowspan");
                    int width = Span("colspan");
                    if (height > 10000 || width > 256 || col + width > 256)
                        throw new InvalidDataException("Wiki table span exceeds supported bounds.");
                    for (int y = r; y < Math.Min(rows.Length, r + height); y++)
                        for (int x = col; x < col + width; x++) grid[(y, x)] = cell;
                    col += width;
                }
                var row = grid.Where(p => p.Key.Row == r).OrderBy(p => p.Key.Col).ToArray();
                if (cells.Length == 0) continue;
                if (!body && cells.All(c => c.LocalName == "th" && !HasCode(c) && c.QuerySelector("table") is null))
                {
                    foreach (var entry in row)
                    {
                        if (!headers.TryGetValue(entry.Key.Col, out var labels)) headers[entry.Key.Col] = labels = [];
                        var label = Normalize(Plain(entry.Value));
                        if (label.Length > 0 && !labels.Contains(label)) labels.Add(label);
                    }
                    continue;
                }
                body = true;
                string Label(int c) => headers.TryGetValue(c, out var labels) ? string.Join(" / ", labels) : "";
                if (row.Any(p => HasCode(p.Value) || p.Value.QuerySelector("table") is not null))
                {
                    // Code remains byte-for-byte text, with its table column label kept separately.
                    foreach (var entry in row)
                    {
                        if (!visited.Add(entry.Value)) continue;
                        var saved = context;
                        context = string.Join(" / ", new[] { context, Label(entry.Key.Col) }.Where(s => !string.IsNullOrEmpty(s)));
                        if (context.Length == 0) context = null;
                        WalkChildren(entry.Value);
                        context = saved;
                    }
                }
                else
                {
                    Emit("text", string.Join(" | ", row.Select(entry =>
                    {
                        var value = Normalize(Plain(entry.Value));
                        var label = Label(entry.Key.Col);
                        return label.Length == 0 ? value : label + ": " + (value.Length == 0 ? "(empty)" : value);
                    })));
                }
            }
            if (!body && headers.Count > 0)
                Emit("text", string.Join(" | ", headers.OrderBy(h => h.Key).Select(h => string.Join(" / ", h.Value))));
        }
        void Walk(INode node)
        {
            if (node is IElement e)
            {
                if (Regex.IsMatch(e.LocalName, "^h[1-6]$"))
                {
                    int level = e.LocalName[1] - '0';
                    section = Normalize(e.TextContent);
                    anchor = e.QuerySelector("[id]")?.Id ?? e.Id ?? "";
                    headings.RemoveAll(h => h.Level >= level);
                    headings.Add((level, section));
                    Emit("heading", section);
                    return;
                }
                if (e.LocalName == "pre" || e.ClassList.Any(c => c.EndsWith("highlighter-scroller", StringComparison.Ordinal)))
                {
                    Emit("code", e.TextContent.Replace("\r\n", "\n").Trim('\n'));
                    return;
                }
                if (e.ClassList.Contains("biki-messagebox"))
                {
                    var saved = note;
                    note = e.ClassList.Any(c => c is "feature-important" or "feature-warning") ? "warning" : "note";
                    if (!HasCode(e) && e.QuerySelector("table") is null) Emit("text", Plain(e));
                    else WalkChildren(e);
                    note = saved;
                    return;
                }
                if (e.LocalName == "table") { Table(e); return; }
                if (e.LocalName is "p" or "li" or "dt" or "dd" or "figcaption")
                {
                    if (e.QuerySelector("pre,ul,ol,p,table,dl,[class*='highlighter-scroller'],.biki-messagebox") is not null)
                        WalkChildren(e);
                    else Emit("text", Plain(e));
                    return;
                }
            }
            if (node is IText t) Emit("text", t.Data);
            else WalkChildren(node);
        }
        void WalkChildren(INode node)
        {
            var inline = new StringBuilder();
            void Flush() { Emit("text", inline.ToString()); inline.Clear(); }
            foreach (var child in node.ChildNodes)
            {
                if (child is IText text) inline.Append(text.Data);
                else if (child is IElement e)
                {
                    if (e.ClassList.Contains("biki-messagebox") || e.LocalName is
                        "p" or "div" or "table" or "ul" or "ol" or "li" or "dl" or "dt" or "dd" or
                        "pre" or "section" or "article" or "blockquote" or "figure" or "figcaption" or
                        "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
                    { Flush(); Walk(e); }
                    else inline.Append(Plain(e));
                }
            }
            Flush();
        }
        Walk(doc.Body!);
        return blocks.ToArray();
    }
}
