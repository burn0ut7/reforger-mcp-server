using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class LogTools(ConsoleLogs logs)
{
    static object Report(Func<object> action)
    {
        try { return action(); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "log_sessions", ReadOnly = true, OpenWorld = false)]
    [Description("List configured console-log sources, or newest session folders for one source. Workbench and peer profiles use the same reader. Newest folder is not proof of a running process. Only console.log is read; requires logDirectories configuration.")]
    public object Sessions(string? source = null, int limit = 10) => Report(() => logs.Sessions(source, limit));

    [McpServerTool(Name = "log_read", ReadOnly = true, OpenWorld = false)]
    [Description("Read bounded console.log evidence. Default latest scope starts at the latest whole script reload/initial compilation and includes subsequent runtime logs. Repeated reload headings stay in one cycle. Missing boundaries error explicitly; session/tail are explicit alternatives. Counts are physical lines before filtering, not distinct problems; untimestamped lines inherit category/severity. cycle.completionLine is a timing marker, not proof of success. After reload_failed, start with severity:error; broaden if needed. Times are local as logged; header supplies session date/UTC. Case-insensitive category/severity/text filters; regex is opt-in with timeout. For context read session scope from an observed line without filters. Pass returned session, revision and nextLine as startLine with identical filters to page a fixed byte prefix; appends are excluded, overwritten/truncated/replaced prefixes reject revision. Omit revision to refresh. partialLastLineOmitted appears only when an unfinished trailing line was excluded. 80 lines default, max 200 and 24KB entry budget; individual lines clip at 2000 characters. Scans limited to 256MiB/10s. No editor mutations.")]
    public object Read(string source, string? session = null, string scope = "latest", int cycleOffset = 0,
        string? severity = null, string? category = null, string? query = null, bool regex = false,
        int startLine = 1, int limit = 80, string? revision = null, CancellationToken cancellationToken = default)
        => Report(() => logs.Read(source, session, scope, cycleOffset, severity, category, query, regex, startLine, limit, revision, cancellationToken));
}
