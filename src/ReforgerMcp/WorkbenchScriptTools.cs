using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class WorkbenchScriptTools(WorkbenchSession scripts)
{
    private static async Task<object> Report(Func<Task<object>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }
    [McpServerTool(Name = "workbench_script_status", ReadOnly = true, OpenWorld = false)]
    [Description("Observe Workbench processes, windows and compiler buffer; historical diagnostics are not a fresh compile gate.")]
    public Task<object> Status(CancellationToken cancellationToken = default) => Report(() => scripts.Status(cancellationToken));

    [McpServerTool(Name = "workbench_resource_leak_assertion", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("action=status|suppress for process-local Resources are leaking assertion only; logs remain active. Automatic monitor also suppresses it. Existing dialogs still need a response.")]
    public Task<object> ResourceLeakAssertion(string action, int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.ResourceLeakAssertion(action, processId, cancellationToken));

    [McpServerTool(Name = "workbench_reload_scripts", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Stop embedded Play, verify stopped, then reload scripts with World Editor open. Requires saved tabs; never auto-saves. Returns fresh compile diagnostics or explicit unavailability.")]
    public Task<object> Reload(int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.Reload(processId, cancellationToken));

    [McpServerTool(Name = "workbench_open_world", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Open .ent resource or relative/absolute path; replaces clean world. Refuses Play or unsaved changes; returns world identity.")]
    public Task<object> OpenWorld(string resourceName, int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.OpenWorld(resourceName, processId, cancellationToken));

    [McpServerTool(Name = "workbench_open_prefab", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Open GUID-qualified prefab resource in definition mode; refuses Play or unsaved changes. Returns prefabResource/rootEntityId for inspection and save.")]
    public Task<object> OpenPrefab(string resourceName, int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.OpenWorld(resourceName, processId, cancellationToken, prefab: true));

    [McpServerTool(Name = "workbench_play_status", ReadOnly = true, OpenWorld = false)]
    [Description("Read current embedded Play flag; not scenario readiness.")]
    public Task<object> PlayStatus(int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.Play("status", processId, cancellationToken));

    [McpServerTool(Name = "workbench_play", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("action=start|stop embedded Play, idempotently. Start requires saved clean world and preserves Play settings; stop discards runtime-only changes.")]
    public Task<object> Play(string action, int processId, CancellationToken cancellationToken = default)
        => Report(() => action is "start" or "stop" ? scripts.Play(action, processId, cancellationToken)
            : throw new ArgumentException("action must be start or stop; use workbench_play_status to observe."));

    [McpServerTool(Name = "workbench_operation", ReadOnly = true, OpenWorld = false)]
    [Description("Poll semantic operationId or reread stored result on this server. details=true returns original full runtime diagnostics without execution. Last 16 operations retained until restart; rereading is not fresh observation.")]
    public Task<object> Operation(string operationId, bool details = false, CancellationToken cancellationToken = default)
        => Report(() => scripts.Operation(operationId, cancellationToken, details));
}


