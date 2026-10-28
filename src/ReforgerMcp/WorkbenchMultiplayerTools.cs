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
    [Description("Inspect the loaded dedicated-server or Peer Tool plugin using tool=dedicated or peers and processId from workbench_script_status. Returns live settings, configured peers, processes observed through the plugin's own handles, redacted command-line arguments and candidate log directories/sessions. Running does not prove connection or successful player spawn. Candidate logs require checking their headers and connection events. Uses direct native hooks on Workbench's main thread. Requires the exact supported binary profile and an open World Editor. May return pending; poll workbench_operation on the same connection. Resolve modal dialogs first. No registry settings are substituted for live state.")]
    public Task<object> Status(string tool, int processId, CancellationToken cancellationToken = default)
        => Report(() => workbench.Multiplayer(tool, "status", processId, null, null, null, cancellationToken));

    [McpServerTool(Name = "workbench_multiplayer", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Use the existing loaded Workbench plugin directly: tool=dedicated or peers; action=start, stop, or configure (Peer Tool only). Requires processId from workbench_script_status. Dedicated start requires workbench.runtimePort and the current managed adapter installed with workbench_runtime_setup; it validates mounted addon paths before dispatch. Dedicated start calls its Run event and opens its normal settings dialog: use window tools to choose Run, Start only clients, or Close and answer any script authorization prompts. Peers start calls only PeerPlugin's normal startup event, preserving launch delay and saved peer options; it does not start Workbench Play or a server. Have a compatible server listening before starting peers. Stop calls that plugin's normal end event to cancel delayed starts and kill its owned processes. Existing running groups are not launched twice. Keep the connection; pending results use workbench_operation. An invoked result proves dispatch only: inspect multiplayer status and logs for actual readiness/connections. Never replay an uncertain launch, including after reconnecting to MCP. Changes through individual settings are explicit; defaults are preserved. Native windows and game clients may activate.")]
    public Task<object> Execute(string tool, string action, int processId, CancellationToken cancellationToken = default)
        => Report(() => action is "start" or "stop" or "configure"
            ? workbench.Multiplayer(tool, action, processId, null, null, null, cancellationToken)
            : throw new ArgumentException("action must be start, stop or configure."));

    [McpServerTool(Name = "workbench_multiplayer_setting", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Read or deliberately update one LIVE plugin setting through engine reflection. tool=dedicated or peers, processId from workbench_script_status. Omit value to read; provide value and a reason to change a stopped session. Dedicated fields: world (empty uses current world; direct-world config only), executable, profile, parameters, runPeers, maxFPS, peerExecutable. Peer Tool fields: executable, launchDelay. Both expose peerCount read-only and peer.INDEX.FIELD (zero-based): title, enabled, profile, parameters, autoJoin, autoReconnect, disableTimeout, windowed, forceUpdate, noFocus, maxFPS, width, height, x, y. Strings are raw values; bools true/false; numbers decimal integers. Existing peer entries only; use add_item on the array row in Peer Tool configure or the dedicated start dialog to add entries. Removing entries and changing configuration variants require supported dialog controls. Other fields stay unchanged. Updates affect the loaded plugin instance, not the registry; Workbench UI may subsequently persist it. May return pending; poll workbench_operation. Never change configuration during an unsettled launch. Parameters may contain sensitive data; avoid adding secrets unnecessarily.")]
    public Task<object> Setting(string tool, string field, int processId, string? value = null, string? reason = null, CancellationToken cancellationToken = default)
        => Report(() => workbench.Multiplayer(tool, value is null ? "get" : "set", processId, field, value, reason, cancellationToken));
}
