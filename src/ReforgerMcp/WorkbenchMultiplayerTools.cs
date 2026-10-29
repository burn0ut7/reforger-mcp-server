using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class WorkbenchMultiplayerTools(WorkbenchSession workbench)
{
    private static async Task<object> Report(Func<Task<object>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "workbench_multiplayer_status", ReadOnly = true, OpenWorld = false)]
    [Description("Read live settings, processes and log candidates for tool=dedicated|peers. Running does not imply connected.")]
    public Task<object> Status(string tool, int processId, CancellationToken cancellationToken = default)
        => Report(() => workbench.Multiplayer(tool, "status", processId, null, null, null, cancellationToken));

    [McpServerTool(Name = "workbench_multiplayer", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("tool=dedicated|peers; action=start|stop|configure (configure: peers only). Dedicated start opens its normal dialog and requires installed adapter/runtimePort. Peers start does not start server/Play. Stop terminates owned processes and delayed launches. Dispatch is not readiness.")]
    public Task<object> Execute(string tool, string action, int processId, CancellationToken cancellationToken = default)
        => Report(() => action is "start" or "stop" or "configure"
            ? workbench.Multiplayer(tool, action, processId, null, null, null, cancellationToken)
            : throw new ArgumentException("action must be start, stop or configure."));

    [McpServerTool(Name = "workbench_multiplayer_setting", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Read live setting by omitting value; write requires value+reason and stopped session. tool=dedicated|peers. Existing peer entries only; add through dialog add_item. Strings raw, bools true/false, integers decimal. UI may persist changes.")]
    public Task<object> Setting(string tool, [Description("dedicated: world,executable,profile,parameters,runPeers,maxFPS,peerExecutable. peers: executable,launchDelay. Both: peerCount (read-only), peer.INDEX.FIELD (zero-based): title,enabled,profile,parameters,autoJoin,autoReconnect,disableTimeout,windowed,forceUpdate,noFocus,maxFPS,width,height,x,y.")] string field, int processId, string? value = null, string? reason = null, CancellationToken cancellationToken = default)
        => Report(() => workbench.Multiplayer(tool, value is null ? "get" : "set", processId, field, value, reason, cancellationToken));
}
