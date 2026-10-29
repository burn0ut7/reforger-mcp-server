using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class EngineTools(NativeEngine engine)
{
    private static async Task<T> Report<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException))
        { throw new McpException(ex.Message); }
    }
    [McpServerTool(Name = "engine_targets", ReadOnly = true, OpenWorld = false)]
    [Description("List configured native binaries and analysis readiness.")]
    public Task<object> Targets(CancellationToken cancellationToken) => Report(() => engine.Targets(cancellationToken));

    [McpServerTool(Name = "engine_prepare", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Prepare persistent Ghidra analysis; returns jobId. rebuild retires existing analysis even if rebuilding fails.")]
    public Task<JobInfo> Prepare(string target, bool rebuild = false, CancellationToken cancellationToken = default) => Report(() => engine.Prepare(target,cancellationToken,rebuild));

    [McpServerTool(Name = "engine_job", ReadOnly = true, OpenWorld = false)]
    [Description("Poll native-analysis jobId; only ready means analysis completed.")]
    public JobInfo Job(string jobId) => engine.JobStatus(jobId,false);

    [McpServerTool(Name = "engine_cancel", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Request cancellation of native-analysis jobId.")]
    public JobInfo Cancel(string jobId) => engine.JobStatus(jobId,true);

    [McpServerTool(Name = "engine_diagnostics", ReadOnly = true, OpenWorld = false)]
    [Description("Page current preparation warnings/errors; completed analysis may have partial coverage. limit=1-100.")]
    public Task<object> Diagnostics(string target, int offset = 0, int limit = 20, [Description("Previous identity.analysisKey; rejects changed analysis while paging.")] string? analysisKey = null, CancellationToken cancellationToken = default)
        => Report(() => engine.Diagnostics(target,offset,limit,analysisKey,cancellationToken));

    [McpServerTool(Name = "engine_search", ReadOnly = true, OpenWorld = false)]
    [Description("Search analyzed strings or symbols: kind=strings|symbols; query=1-200 characters, case-insensitive all-term match. limit=1-100.")]
    public Task<EngineResult> Search(string target, string query, string kind = "strings", int offset = 0, int limit = 20,
        [Description("Previous identity.analysisKey; rejects changed analysis while paging.")] string? analysisKey = null,
        CancellationToken cancellationToken = default) => Report(() => engine.Query(target,"search",query,kind,"","",offset,limit,analysisKey,cancellationToken));

    [McpServerTool(Name = "engine_function", ReadOnly = true, OpenWorld = false)]
    [Description("Inspect function at hexadecimal address. section=overview|code|context|callers|callees|instructions|types; limit=1-100. codeSha256 identifies code-page content; codeArtifact holds full pseudocode. Inferred code/types; imports have no body.")]
    public Task<EngineResult> Function(string target, string address, string section = "overview", int offset = 0, int limit = 60,
        [Description("Previous identity.analysisKey; rejects changed analysis while paging.")] string? analysisKey = null, CancellationToken cancellationToken = default) => Report(() => engine.Query(target,"function","","",address,section,offset,limit,analysisKey,cancellationToken));

    [McpServerTool(Name = "engine_references", ReadOnly = true, OpenWorld = false)]
    [Description("Page static references at hexadecimal address; limit=1-100. Includes up to 100 outgoing references and 16 analyzed bytes (not live memory). Unresolved dynamic targets may be absent.")]
    public Task<EngineResult> References(string target, string address, int offset = 0, int limit = 30,
        [Description("Previous identity.analysisKey; rejects changed analysis while paging.")] string? analysisKey = null, CancellationToken cancellationToken = default) => Report(() => engine.Query(target,"references","","",address,"",offset,limit,analysisKey,cancellationToken));
}
