using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

[McpServerToolType]
public sealed class WorkbenchTools(Workbench workbench, PakStore store)
{
    private static object Report(Func<object> action)
    {
        try { return action(); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException))
        { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "workbench_status", ReadOnly = true, OpenWorld = false)]
    [Description("Inspect Workbench processes and disk catalogue readiness in one call. Returns workbench (process/window observations; project readiness unverified) and pak (freshness, source coverage and revision). Does not launch Workbench or prepare the catalogue.")]
    public async Task<object> Status(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observed = workbench.Status();
            var catalogue = await store.Status(cancellationToken);
            return new { workbench = observed, pak = catalogue };
        }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException))
        { throw new McpException(ex.Message); }
    }

    [McpServerTool(Name = "workbench_terminate", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Force-terminate explicit processId; unsaved work is lost.")]
    public object Terminate(int processId) => Report(() => workbench.Terminate(processId));

    [McpServerTool(Name = "workbench_open_project", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Launch existing absolute .gproj; refuses if Workbench is running. Started process does not establish project readiness.")]
    public object OpenProject(
        [Description("Absolute path to the addon .gproj file; paths with spaces are supported.")] string projectPath,
        CancellationToken cancellationToken = default) => Report(() => workbench.OpenProject(projectPath, cancellationToken));
}
