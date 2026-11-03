namespace ReforgerMcp;

public sealed partial class WorkbenchSession
{
    internal volatile object AutomaticResourceLeakSuppression = new { state = "not_started" };

    // Startup maintenance must not activate Qt's accessibility bridge. Even a
    // top-level FromHandle enables later UIA event processing while users edit.
    // The native owner validates the application/build and queues on its GUI
    // thread; this flag does not save, reload, close or mutate authored content.
    internal Task<object> SuppressResourceLeaksAutomatically(int processId, CancellationToken ct) => Run(token =>
    {
        using var process = SelectProcess();
        if (process.Id != processId) throw new WorkbenchBusyException("Workbench process changed before startup maintenance.");
        if (process.MainWindowHandle == 0) throw new WorkbenchBusyException("Waiting for the Workbench main window.");
        return NativeWorkbench.Execute(process, token, resourceLeakAssertion: true);
    }, ct, TimeSpan.FromSeconds(190));

    public Task<object> ResourceLeakAssertion(string action, int processId, CancellationToken ct)
    {
        if (action is not ("status" or "suppress")) throw new ArgumentException("action must be status or suppress.");
        if (processId <= 0) throw new ArgumentException("processId must come from workbench_script_status.");
        return Start(token =>
        {
            using var process = SelectProcess();
            if (process.Id != processId) throw new InvalidOperationException("Workbench process changed; inspect workbench_script_status.");
            var editorWindows = Windows(process.Id);
            if (!editorWindows.Any(w => w.Current.AutomationId == ResourceManager))
                throw Blocked(process, "Waiting for the Resource Manager before assertion suppression.");
            if (editorWindows.Any(IsModal) || editorWindows.Any(w => !w.Current.IsEnabled))
                throw Blocked(process, "Resolve existing editor dialogs before changing or observing assertion suppression.");
            return NativeWorkbench.Execute(process, token, resourceLeakAssertion: action == "suppress");
        }, ct);
    }
}
