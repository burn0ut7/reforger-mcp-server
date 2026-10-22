using System.ComponentModel;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

public sealed partial class WorkbenchSession
{
    internal Task<object> History(int processId, string expectedWorld, string action, CancellationToken ct)
    {
        if (action is not ("history" or "undo" or "redo" or "save_world")) throw new ArgumentException("Unsupported world operation.");
        if (processId <= 0 || string.IsNullOrWhiteSpace(expectedWorld) || !expectedWorld.StartsWith('$') ||
            !expectedWorld.Contains(':') || !expectedWorld.EndsWith(".ent", StringComparison.Ordinal) ||
            expectedWorld.Any(char.IsControl) || Encoding.UTF8.GetByteCount(expectedWorld) > 240)
            throw new ArgumentException("Supply the observed processId and exact loadedWorld (at most 240 UTF-8 bytes).");
        return Start(token =>
        {
            using var process = SelectProcess();
            if (process.Id != processId) throw new InvalidOperationException("Workbench process changed.");
            var windows = Windows(process.Id);
            RequiredWindow(windows, ResourceManager); RequiredWindow(windows, "Arma4WorkbenchApp.worldEditorMain");
            if (windows.Any(IsModal) || windows.Any(w => !w.Current.IsEnabled))
                throw Blocked(process, "Resolve dialogs before accessing editor history.");
            if (action != "history") editObservations.Clear();
            return NativeWorkbench.Execute(process, token, entity: new(action, expectedWorld, "", "", [0,0,0], [0,0,0]));
        }, ct);
    }
}

[McpServerToolType]
public sealed class WorkbenchHistoryTools(WorkbenchSession session)
{
    [McpServerTool(Name = "workbench_save_world", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Save the existing named world through the editor's resource save owner, without UI traversal. Requires exact loadedWorld and processId. Refuses Play, prefab mode and active edit actions. Returns native save result and remaining modified state. Saves the editor's world/layer changes, including user edits; not Save As or prefab export. Invalidates edit tokens. Poll pending workbench_operation; never replay after uncertainty without inspecting state and files.")]
    public Task<object> Save(int processId, string expectedWorld, CancellationToken cancellationToken = default)
        => Run(processId, expectedWorld, "save_world", cancellationToken);

    [McpServerTool(Name = "workbench_history_status", ReadOnly = true, OpenWorld = false)]
    [Description("Observe the normal World Editor undo-history position and availability through its owning functions. Requires exact loadedWorld; refuses Play, dialogs and prefab mode. History includes user actions, not just MCP edits. Pending uses workbench_operation.")]
    public Task<object> Status(int processId, string expectedWorld, CancellationToken cancellationToken = default)
        => Run(processId, expectedWorld, "history", cancellationToken);

    [McpServerTool(Name = "workbench_history", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Undo or redo one current World Editor history action through the owning editor function. action is undo or redo. Includes user actions; inspect history and current state before acting. Invalidates edit tokens, leaves world unsaved, and reports observed index change. No UI traversal or automatic retry. Requires exact loadedWorld; refuses Play, dialogs and prefab mode. Pending uses workbench_operation.")]
    public Task<object> Edit(int processId, string expectedWorld, string action, CancellationToken cancellationToken = default)
    {
        if (action is not ("undo" or "redo")) throw new McpException("action must be undo or redo.");
        return Run(processId, expectedWorld, action, cancellationToken);
    }

    private async Task<object> Run(int processId, string expectedWorld, string action, CancellationToken ct)
    {
        try { return await session.History(processId, expectedWorld, action, ct); }
        catch (Exception e) when (e is not (McpException or OperationCanceledException)) { throw new McpException(e.Message); }
    }
}
