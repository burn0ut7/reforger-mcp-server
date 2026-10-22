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
    [Description("List configured game, diagnostic, workbench and server binaries with current identity and preparation state. Reads installed files; does not start the game.")]
    public Task<object> Targets(CancellationToken cancellationToken) => Report(() => engine.Targets(cancellationToken));

    [McpServerTool(Name = "engine_prepare", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Prepare persistent Ghidra analysis of one installed native executable. Returns a job ID immediately; poll engine_job. May take tens of minutes. Existing current analysis is reused unless rebuild=true explicitly retires it and builds a new project. A failed/cancelled rebuild never returns to the retired analysis. Never modifies the installed executable. No automatic retries or alternate backend.")]
    public Task<JobInfo> Prepare(string target, bool rebuild = false, CancellationToken cancellationToken = default) => Report(() => engine.Prepare(target,cancellationToken,rebuild));

    [McpServerTool(Name = "engine_job", ReadOnly = true, OpenWorld = false)]
    [Description("Read preparation state, latest log progress, failures and evidence location. Only ready means completed analysis; analysis warnings/errors remain relevant.")]
    public JobInfo Job(string jobId) => engine.JobStatus(jobId,false);

    [McpServerTool(Name = "engine_cancel", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Request cancellation of a preparation job, including its Ghidra process. Poll engine_job for terminal state. Incomplete results are never published as ready.")]
    public JobInfo Cancel(string jobId) => engine.JobStatus(jobId,true);

    [McpServerTool(Name = "engine_diagnostics", ReadOnly = true, OpenWorld = false)]
    [Description("Read paged warnings/errors from the current target's preparation log. Completed analysis can still have partial type/instruction coverage; combine with per-function warnings.")]
    public Task<object> Diagnostics(string target, int offset = 0, int limit = 20, string? analysisKey = null, CancellationToken cancellationToken = default)
        => Report(() => engine.Diagnostics(target,offset,limit,analysisKey,cancellationToken));

    [McpServerTool(Name = "engine_search", ReadOnly = true, OpenWorld = false)]
    [Description("Search defined engine strings or database symbols. Case-insensitive, all whitespace-separated terms must occur in one entry. Try replication, prefab, resource or an exact error phrase. String hits include up to eight references with containing function addresses. Native implementation evidence is not a supported mod API. Paged results include coverage and truncation flags.")]
    public Task<EngineResult> Search(string target, string query, string kind = "strings", int offset = 0, int limit = 20,
        [Description("Pass identity.analysisKey from the previous response when paging to reject a changed binary, configuration or saved analysis revision.")] string? analysisKey = null,
        CancellationToken cancellationToken = default) => Report(() => engine.Query(target,"search",query,kind,"","",offset,limit,analysisKey,cancellationToken));

    [McpServerTool(Name = "engine_function", ReadOnly = true, OpenWorld = false)]
    [Description("Inspect the function containing a hexadecimal memory address. Sections: overview (signatures/counts/warnings), code (pseudocode lines), context (strings/named references/computed calls), callers, callees, instructions (starting at the selected address), types (inferred variables). offset/limit page the chosen section. Independent analysis results can differ: combine code pages only with matching codeSha256. Complete single-run pseudocode is saved as codeArtifact under evidenceDirectory. Read context and warnings before explaining behavior; use instructions when inferred C is ambiguous. External import addresses do not contain native function bodies.")]
    public Task<EngineResult> Function(string target, string address, string section = "overview", int offset = 0, int limit = 60,
        string? analysisKey = null, CancellationToken cancellationToken = default) => Report(() => engine.Query(target,"function","","",address,section,offset,limit,analysisKey,cancellationToken));

    [McpServerTool(Name = "engine_references", ReadOnly = true, OpenWorld = false)]
    [Description("Find paged inbound references to a hexadecimal symbol/string address, plus up to 100 outgoing references at that exact address. Outgoing references can resolve defined pointer or virtual-table slots; inspect outgoingReferencesTruncated. memoryPreview reports up to 16 analyzed database bytes at the selected address, block-bounded, with explicit unavailable status; it is not live memory. Use after symbol search or when a string's inline reference list is truncated; follow returned functionAddress into engine_function. Only resolved static references are known; no outgoing hit does not prove a runtime target is absent.")]
    public Task<EngineResult> References(string target, string address, int offset = 0, int limit = 30,
        string? analysisKey = null, CancellationToken cancellationToken = default) => Report(() => engine.Query(target,"references","","",address,"",offset,limit,analysisKey,cancellationToken));
}
