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
    [Description("Read the configured Workbench process identity, native editor windows/dialogs and current compiler output. Output reflects the editor buffer; it does not confirm reload completion or project readiness. No scripted bridge. Requires an accessible Windows desktop editor.")]
    public Task<object> Status(CancellationToken cancellationToken = default) => Report(() => scripts.Status(cancellationToken));

    [McpServerTool(Name = "workbench_resource_leak_assertion", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Observe (action=status) or suppress (action=suppress) only the native 'Resources are leaking! Check log!' assertion at GameApp.cpp:1287. Uses the engine's per-assertion suppression flag on the main thread; no executable patch, global assertion disable, or popup automation. Leak logging/checks remain enabled; suppression does not fix the leak. Requires processId from workbench_script_status, supported Workbench/Qt binaries, and no existing modal dialog. Returns observed/suppressed with flag readback, or pending to poll workbench_operation on this connection. Persists until Workbench exits, including manual editor actions and MCP reconnects. Also enabled automatically before MCP reload, world/prefab opening, Play start/stop and native UI actions. While the MCP server is running, a background monitor activates suppression once each configured Workbench process is ready. workbench_script_status includes its last activation result; status itself never enables suppression. Does not dismiss an already-open assertion or affect separate game/server processes.")]
    public Task<object> ResourceLeakAssertion(string action, int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.ResourceLeakAssertion(action, processId, cancellationToken));

    [McpServerTool(Name = "workbench_reload_scripts", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Validate and reload all Game and Workbench script modules through the owning native routine. Requires processId from workbench_script_status, the exact supported Workbench/Qt binaries, and Resource Manager with optional Script Editor/World Editor. Automatically stops embedded Play when needed and verifies it stopped before reloading; leaves World Editor open. Resolve unsaved tabs/dialogs and unsupported editor types first. Resets runtime state; does not save files, accept dialogs or control separate game clients. Pre-dispatch editor refusals return blocked with dispatched:false and bounded blockerInspection; a pending-operation refusal supplies its poll call. Completed reloads include fresh process-owned log diagnostics or an explicit unavailable reason. An unconfirmed Stop result returns reload_not_started without dispatching Reload; inspect its Play result. Stop and Reload share one serialized operation. Returns reloaded/reload_failed, or pending with operationId after 500 ms: inspect dialogs and poll workbench_operation on this connection. No standalone validation, menu/keyboard execution or foreground requirement. Engine-created windows may activate. On interruption inspect logs/state; never replay an uncertain mutation.")]
    public Task<object> Reload(int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.Reload(processId, cancellationToken));

    [McpServerTool(Name = "workbench_open_world", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Open one .ent world in the running configured Workbench through the native hook on its main thread. Accepts a resource name such as {GUID}worlds/path.ent, a relative resource path, or an absolute path accessible to the loaded project. Creates World Editor when needed. Requires processId from workbench_script_status and exact supported Workbench/Qt binaries. Refuses play mode, unsaved changes, modal dialogs and other editor types. Does not save, discard, click, send keys or require foreground focus. Engine-created windows may activate; returned foreground observations are sampled. Returns opened/open_failed plus the native current world path, or pending with operationId after 500 ms. For pending inspect workbench_windows, answer dialogs, then poll workbench_operation on the same server connection within 180 seconds. Opening replaces the clean current world; it may load project code/resources. After uncertainty inspect logs/screenshots; never blindly replay. Shares the native-operation claim with script reload.")]
    public Task<object> OpenWorld(string resourceName, int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.OpenWorld(resourceName, processId, cancellationToken));

    [McpServerTool(Name = "workbench_open_prefab", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Open an existing GUID-qualified prefab resource ({GUID}path.et) in native World Editor prefab-definition mode. Replaces the clean current editor document; refuses Play and unsaved changes. Returns the verified prefab resource and rootEntityId only after native mode and resource identity checks. Poll pending workbench_operation on this connection; never replay an uncertain open.")]
    public Task<object> OpenPrefab(string resourceName, int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.OpenWorld(resourceName, processId, cancellationToken, prefab: true));

    [McpServerTool(Name = "workbench_play_status", ReadOnly = true, OpenWorld = false)]
    [Description("Observe World Editor's native Play flag on its main thread. Requires processId from workbench_script_status and the exact supported Workbench/Qt binaries. Does not open a world or start play. Returns observed with isPlaying, world_unavailable or an explicit rejection; may return pending, to poll with workbench_operation. Shares the native operation lane; resolve modal dialogs first. State is a point-in-time observation, not proof of game/scenario readiness or separate game-client state.")]
    public Task<object> PlayStatus(int processId, CancellationToken cancellationToken = default)
        => Report(() => scripts.Play("status", processId, cancellationToken));

    [McpServerTool(Name = "workbench_play", ReadOnly = false, Destructive = true, OpenWorld = true)]
    [Description("Start or stop the current World Editor Play session using action=start or stop and processId from workbench_script_status. Checks and changes state on the editor thread: repeated start/stop is a no-op, never a toggle. Start requires a saved clean world and honors the editor's existing Play settings (including viewport/fullscreen, camera and save-before-play); it runs project code and may use game online services. Stop ends runtime simulation through the normal editor lifecycle. Does not change save settings, answer dialogs, send keyboard input, or control separate game clients. Native save-before-play can save the clean world. Exact supported Workbench/Qt binaries required. Returns started/stopped, already_playing/already_stopped, transition_failed or explicit rejection, with observed wasPlaying/isPlaying. These flags do not prove scenario readiness. Pending results must be polled with workbench_operation on the same connection; inspect/resolve dialogs with window tools. On uncertainty inspect state/logs, never blindly replay. Native behavior can activate windows.")]
    public Task<object> Play(string action, int processId, CancellationToken cancellationToken = default)
        => Report(() => action is "start" or "stop" ? scripts.Play(action, processId, cancellationToken)
            : throw new ArgumentException("action must be start or stop; use workbench_play_status to observe."));

    [McpServerTool(Name = "workbench_operation", ReadOnly = true, OpenWorld = false)]
    [Description("Poll the operationId returned by a pending workbench_open_world, workbench_reload_scripts, workbench_play, workbench_play_status, workbench_runtime_*, workbench_multiplayer*, workbench_entity, workbench_create_entity or workbench_save_prefab on this same server connection. Returns pending or the original completion result/error. Pending responses include dialogInspection: observed non-editor windows/text/buttonIds, or an explicit unavailable reason. Inspection waits at most one second and does not prove the operation is blocked by a dialog. Use observed buttonIds with workbench_window_action after deciding the response; nothing is answered automatically. Unchanged tokens survive polling. Use workbench_windows/screenshots for more detail. Never repeat the mutation to poll. Last 16 native operations retain their original results/errors on this connection; oldest completed entries expire at capacity. Server restart clears history. Stored observations and action IDs do not become fresh when reread. Native operations have a 180-second deadline; runtime NET requests have a 15-second deadline and do not cancel engine code on timeout.")]
    public Task<object> Operation(string operationId, CancellationToken cancellationToken = default)
        => Report(() => scripts.Operation(operationId, cancellationToken));
}


