using System.Text.RegularExpressions;

namespace ReforgerMcp;

public sealed partial class WikiStore
{
    private static readonly HashSet<string> QuestionWords = new(
        "a an the to of for in on with and or how do does did i we my can could should would when what which is are be it this that from as at into using use want need new existing avoid arma reforger mod modding create creating make making add adding".Split(' '),
        StringComparer.OrdinalIgnoreCase);

    private static string Word(string word)
    {
        var lower = word.ToLowerInvariant();
        // Keep underscore and camel-case names intact; literal API lookup remains wiki_search.
        if (word.Contains('_') || word.Skip(1).Any(char.IsUpper)) return lower;
        if (lower == "scripting") return "script";
        if (lower.StartsWith("optimi", StringComparison.Ordinal)) return "optimise";
        if (lower.StartsWith("initiali", StringComparison.Ordinal)) return "initialise";
        if (lower.Length > 5 && lower.EndsWith("ies", StringComparison.Ordinal)) return lower[..^3] + "y";
        if (lower.Length > 5 && lower.EndsWith("sses", StringComparison.Ordinal)) return lower[..^2];
        if (lower.Length > 4 && lower.EndsWith('s') && !lower.EndsWith("ss", StringComparison.Ordinal)) return lower[..^1];
        return lower;
    }
    private static string[] Words(string text) => Regex.Matches(
        Regex.Replace(Regex.Replace(text, @"\bset[\s-]+up\b", "setup", RegexOptions.IgnoreCase),
            @"\badd[\s-]+on(s?)\b", "addon$1", RegexOptions.IgnoreCase), @"[\p{L}\p{N}_]+")
        .Select(m => Word(m.Value)).Where(w => !QuestionWords.Contains(w)).ToArray();

    public Task<WikiResearchResult> Research(string query, int limit, CancellationToken ct) => Locked(async () =>
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 500 || limit is < 1 or > 5)
            throw new ArgumentException("Supply a question of 1-500 characters and limit 1-5.");
        var terms = Words(query).Distinct().ToArray();
        if (terms.Length == 0) throw new ArgumentException("Include a specific modding topic, such as replication, user action or prefab.");
        // Two documented terminology bridges, exposed in results; no inferred API aliases.
        var expanded = terms.SelectMany(t => t switch
        {
            "optimise" => new[] { "performance" },
            "initialise" => new[] { "instantiation" },
            _ => Array.Empty<string>()
        }).Except(terms).ToArray();
        var wanted = terms.Concat(expanded).ToArray();
        var s = await Load(null, ct);
        var docs = s.Pages.Where(p => p.Namespace != 14).Select(p =>
        {
            ct.ThrowIfCancellationRequested();
            var title = Words(p.Title.Replace("Arma Reforger:", "", StringComparison.Ordinal));
            var headings = Words(string.Join(" ", p.Passages.Where(b => b.Kind == "heading").Select(b => b.Text)));
            var body = Words(string.Join(" ", p.Passages.Where(b => b.Kind != "heading").Select(b => b.Text)));
            var passages = p.Passages.Where(b => b.Kind != "heading")
                .Select(b => Words(b.SectionPath + " " + b.Text).ToHashSet()).ToArray();
            return new { Page = p, Title = title.ToHashSet(), Headings = headings.ToHashSet(),
                Frequencies = body.GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count()), Length = body.Length, Passages = passages };
        }).ToArray();
        double average = Math.Max(1, docs.Select(d => (double)d.Length).DefaultIfEmpty(1).Average());
        var weights = wanted.ToDictionary(t => t, t =>
        {
            int count = docs.Count(d => d.Title.Contains(t) || d.Headings.Contains(t) || d.Frequencies.ContainsKey(t));
            return Math.Log(1 + (docs.Length - count + 0.5) / (count + 0.5)) * (expanded.Contains(t) ? 0.7 : 1);
        });
        double BlockScore(WikiPassage b)
        {
            var words = Words(b.Text).ToHashSet();
            var path = Words(b.SectionPath).ToHashSet();
            return wanted.Sum(t => weights[t] * (words.Contains(t) ? 1 : 0) + weights[t] * (path.Contains(t) ? 0.5 : 0));
        }
        var ranked = docs.Select(d =>
        {
            double score = wanted.Sum(t =>
            {
                double frequency = d.Frequencies.GetValueOrDefault(t);
                // Saturating term frequency, mild length normalization, and explicit title/heading fields.
                double body = frequency * 2.2 / (frequency + 1.2 * (0.85 + 0.15 * d.Length / average));
                return weights[t] * (body + (d.Title.Contains(t) ? 6 : 0) + (d.Headings.Contains(t) ? 2 : 0));
            });
            bool Present(string t) => d.Title.Contains(t) || d.Headings.Contains(t) || d.Frequencies.ContainsKey(t);
            int covered = terms.Count(t => Present(t) || (t == "optimise" && Present("performance")) ||
                (t == "initialise" && Present("instantiation")));
            // A strong match for one common word must not outrank coverage of the whole question.
            score *= Math.Pow((double)covered / terms.Length, 2);
            // Prefer related concepts within one passage and its heading path over scattered page matches.
            int together = d.Passages.Select(words => terms.Count(t => words.Contains(t) || d.Title.Contains(t) ||
                (t == "optimise" && (words.Contains("performance") || d.Title.Contains("performance"))) ||
                (t == "initialise" && (words.Contains("instantiation") || d.Title.Contains("instantiation"))))).DefaultIfEmpty(0).Max();
            score *= 0.25 + 0.75 * Math.Pow((double)together / terms.Length, 2);
            return new { Doc = d, Score = score };
        }).Where(d => d.Score > 0).OrderByDescending(d => d.Score)
            .ThenBy(d => d.Doc.Page.Title, StringComparer.Ordinal).ToArray();
        var pages = ranked.Take(limit).Select(item =>
        {
            var p = item.Doc.Page;
            var candidates = p.Passages.Where(b => b.Kind != "heading")
                .Select(b => new { Block = b, Score = BlockScore(b) }).Where(b => b.Score > 0)
                .OrderByDescending(b => b.Score).ThenBy(b => b.Block.Number).Select(b => b.Block).ToArray();
            var primary = candidates.FirstOrDefault() ?? p.Passages.FirstOrDefault(b => b.Kind != "heading");
            var selected = new List<WikiPassage>();
            if (primary is not null) selected.Add(primary);
            var warnings = primary is null ? [] : ApplicableWarnings(p, Array.IndexOf(p.Passages, primary));
            foreach (var warning in warnings)
                if (selected.Count < 3 && !selected.Contains(warning)) selected.Add(warning);
            var code = candidates.FirstOrDefault(b => b.Kind == "code" && !(b.Context?.Contains("Don't", StringComparison.OrdinalIgnoreCase) ?? false));
            if (code is not null && selected.Count < 3 && !selected.Contains(code)) selected.Add(code);
            foreach (var b in candidates)
                if (selected.Count < 3 && !selected.Contains(b) && selected.All(x => x.Anchor != b.Anchor)) selected.Add(b);
            return new WikiResearchPage(p, selected.OrderBy(b => b.Number).ToArray(),
                terms.Count(t => item.Doc.Title.Contains(t) || item.Doc.Headings.Contains(t) || item.Doc.Frequencies.ContainsKey(t)),
                warnings.Count(w => !selected.Contains(w)), primary?.Number);
        }).ToArray();
        return new WikiResearchResult(s.Revision, s.Completed, terms, ranked.Length, pages, expanded);
    }, ct);
}
