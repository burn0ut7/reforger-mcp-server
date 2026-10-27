using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class WorkbenchTools(Workbench workbench)
{
    private static object Report(Func<object> action)
    {
        try { return action(); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException))
        { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "workbench_status", ReadOnly = true, OpenWorld = false)]
    [Description("Observe Workbench processes and window titles, configured missing paths and this server session's last launch/exit. Does not verify project readiness, read live editor state, or use a scripted bridge. Works without Ghidra. Existing Workbench variants block another launch even if their executable path differs.")]
    public object Status() => Report(workbench.Status);

    [McpServerTool(Name = "workbench_terminate", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Force terminate one explicitly selected Workbench PID after user authorization. Unsaved work is lost. Uses the configured executable process name as a guard; does not terminate children or elevate privileges. For inaccessible/orphaned editors that cannot close normally. Inspect workbench_status afterward; never automatically replay an uncertain result.")]
    public object Terminate(int processId) => Report(() => workbench.Terminate(processId));

    [McpServerTool(Name = "workbench_open_project", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Start the configured Workbench executable directly with -gproj and configured -addonsDir directories. Requires an absolute existing .gproj path. Refuses if Workbench is already running; never switches/closes editors or retries. Returns process-start evidence, NOT project-ready confirmation. Inspect workbench_status and the editor after an uncertain outcome; do not blindly replay. Workbench may load project scripts and access its normal online services. No scripted bridge or native hooks are used.")]
    public object OpenProject(
        [Description("Absolute path to the addon .gproj file; paths with spaces are supported.")] string projectPath,
        CancellationToken cancellationToken = default) => Report(() => workbench.OpenProject(projectPath, cancellationToken));
}
