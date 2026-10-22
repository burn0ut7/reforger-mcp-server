using System.Security.Cryptography;
using System.Text;

namespace ReforgerMcp;

public sealed record ProjectFileInfo(string Path, string Sha256, string Id, string Guid, string Title,
    string[] Dependencies, ProjectModuleDeclaration[] Modules);
public sealed record ProjectModuleDeclaration(string Name, string[] Paths, string? EntryPoint, string Context);

// Reads declarations, not effective inherited configuration or compiler state.
internal static class ProjectFile
{
    private sealed record Token(string Value, bool Quoted);
    public static ProjectFileInfo Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("Project file exceeds 4 MiB.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        return Parse(path, bytes);
    }
    internal static ProjectFileInfo Parse(string path, byte[] bytes)
    {
        var text = PakArchive.Text(bytes).TrimStart('\uFEFF');
        var tokens = Lex(text);
        if (tokens.Count < 3 || tokens[0].Value != "GameProject" || tokens[1].Value != "{")
            throw new InvalidDataException("Unsupported project syntax: expected GameProject block.");
        var stack = new List<string>();
        var values = new Dictionary<string, string>();
        var dependencies = new List<string>();
        var modules = new List<ProjectModuleDeclaration>();
        int depth = 0;
        for (int i = 1; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (!t.Quoted && t.Value == "{") { if (++depth > 64) throw new InvalidDataException("Project nesting exceeds 64."); stack.Add(i > 1 ? tokens[i - 1].Value : "GameProject"); }
            else if (!t.Quoted && t.Value == "}") { if (--depth < 0) throw new InvalidDataException("Unbalanced project."); stack.RemoveAt(stack.Count - 1); if (depth == 0 && i != tokens.Count - 1) throw new InvalidDataException("Trailing project tokens."); }
            else if (depth == 1 && !t.Quoted && t.Value is "ID" or "GUID" or "TITLE")
            {
                if (++i >= tokens.Count || !tokens[i].Quoted || !values.TryAdd(t.Value, tokens[i].Value))
                    throw new InvalidDataException("Missing or duplicate project identity property.");
            }
            else if (depth == 1 && !t.Quoted && t.Value == "Dependencies")
            {
                if (i + 1 >= tokens.Count || tokens[++i].Value != "{") throw new InvalidDataException("Invalid dependencies block.");
                while (++i < tokens.Count && (tokens[i].Quoted || tokens[i].Value != "}"))
                {
                    if (!tokens[i].Quoted)
                        throw new InvalidDataException("Unsupported dependency identity.");
                    dependencies.Add(tokens[i].Value.ToUpperInvariant());
                }
                if (i >= tokens.Count) throw new InvalidDataException("Unclosed dependencies block.");
            }
            else if (!t.Quoted && t.Value == "ScriptModulePathClass" && i + 2 < tokens.Count)
            {
                string name = tokens[i + 1].Value;
                int start = i + 2;
                // An inherited module declaration is retained in context; values below remain local declarations.
                while (start < tokens.Count && (tokens[start].Quoted || tokens[start].Value != "{")) start++;
                int level = 0; var paths = new List<string>(); string? entry = null;
                for (int j = start; j < tokens.Count; j++)
                {
                    var x = tokens[j];
                    if (!x.Quoted && x.Value == "{") level++;
                    else if (!x.Quoted && x.Value == "}" && --level == 0) break;
                    else if (level == 1 && !x.Quoted && x.Value == "EntryPoint" && j + 1 < tokens.Count) entry = tokens[j + 1].Value;
                    else if (level == 1 && !x.Quoted && x.Value == "Paths" && j + 1 < tokens.Count && tokens[j + 1].Value == "{")
                        for (int k = j + 2; k < tokens.Count && tokens[k].Quoted; k++) paths.Add(tokens[k].Value);
                }
                modules.Add(new(name, paths.ToArray(), entry, string.Join("/", stack)));
            }
        }
        if (depth != 0 || !values.TryGetValue("ID", out var id) || string.IsNullOrEmpty(id) ||
            !values.TryGetValue("GUID", out var guid) || !System.Text.RegularExpressions.Regex.IsMatch(guid, "^[0-9A-Fa-f]{16}$"))
            throw new InvalidDataException("Project identity missing/invalid or unbalanced blocks.");
        return new(path, Convert.ToHexString(SHA256.HashData(bytes)), id, guid.ToUpperInvariant(), values.GetValueOrDefault("TITLE", ""), dependencies.Distinct().ToArray(), modules.ToArray());
    }
    private static List<Token> Lex(string text)
    {
        var result = new List<Token>();
        for (int i = 0; i < text.Length;)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') { while (i < text.Length && text[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            { var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal); if (end < 0) throw new InvalidDataException("Unclosed project comment."); i = end + 2; continue; }
            if (c is '{' or '}') { result.Add(new(c.ToString(), false)); i++; continue; }
            if (c == '"')
            {
                i++; var value = new StringBuilder(); bool closed = false;
                while (i < text.Length)
                {
                    c = text[i++]; if (c == '"') { closed = true; break; }
                    if (c == '\\' && i < text.Length && text[i] == '"')
                    {
                        c = text[i++];
                    }
                    if (char.IsControl(c)) throw new InvalidDataException("Control character in project string.");
                    value.Append(c);
                }
                if (!closed) throw new InvalidDataException("Unclosed project string.");
                result.Add(new(value.ToString(), true)); continue;
            }
            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('{' or '}' or '"')) i++;
            result.Add(new(text[start..i], false));
        }
        return result;
    }
}
