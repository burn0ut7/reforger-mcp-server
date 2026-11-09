namespace ReforgerMcp;

public sealed partial class WorkbenchSession
{
    public Task<object> ScriptAuthorization(string action, int processId, CancellationToken ct)
    {
        if (action is not ("status" or "allow")) throw new ArgumentException("action must be status or allow.");
        if (processId <= 0) throw new ArgumentException("processId must come from workbench_script_status.");
        return Start(token =>
        {
            using var process = SelectProcess();
            if (process.Id != processId) throw new InvalidOperationException("Workbench process changed; inspect workbench_script_status.");
            var editorWindows = Windows(process.Id);
            RequiredWindow(editorWindows, ResourceManager);
            if (editorWindows.Any(IsModal) || editorWindows.Any(w => !w.Current.IsEnabled))
                throw Blocked(process, "Resolve an already open dialog before observing or activating script authorization. New prompts are suppressed after activation.");
            return NativeWorkbench.Execute(process, token, scriptAuthorization: action == "allow");
        }, ct);
    }
}
