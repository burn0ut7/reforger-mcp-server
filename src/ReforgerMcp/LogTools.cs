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
    [Description("List configured console.log sources or newest sessions for source; newest does not imply running. Requires logDirectories.")]
    public object Sessions(string? source = null, int limit = 10) => Report(() => logs.Sessions(source, limit));

    [McpServerTool(Name = "log_read", ReadOnly = true, OpenWorld = false)]
    [Description("Read console.log; scope=latest|session|tail. latest starts at the latest compilation cycle. Case-insensitive filters; regex opt-in. Page with returned session/revision/nextLine as startLine and unchanged filters; omit revision to refresh. limit=1-200; 24KB/page, lines clip at 2000 characters. Completion markers do not prove success; timestamps are local.")]
    public object Read(string source, string? session = null, string scope = "latest", int cycleOffset = 0,
        string? severity = null, string? category = null, string? query = null, bool regex = false,
        int startLine = 1, int limit = 80, string? revision = null, CancellationToken cancellationToken = default)
        => Report(() => logs.Read(source, session, scope, cycleOffset, severity, category, query, regex, startLine, limit, revision, cancellationToken));
}
