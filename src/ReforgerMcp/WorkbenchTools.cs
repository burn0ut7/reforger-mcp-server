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
    [Description("List configured Workbench processes; existence does not establish project readiness.")]
    public object Status() => Report(workbench.Status);

    [McpServerTool(Name = "workbench_terminate", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Force-terminate explicit processId; unsaved work is lost. Requires user authorization.")]
    public object Terminate(int processId) => Report(() => workbench.Terminate(processId));

    [McpServerTool(Name = "workbench_open_project", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Launch existing absolute .gproj; refuses if Workbench is running. Started process does not establish project readiness.")]
    public object OpenProject(
        [Description("Absolute path to the addon .gproj file; paths with spaces are supported.")] string projectPath,
        CancellationToken cancellationToken = default) => Report(() => workbench.OpenProject(projectPath, cancellationToken));
}
