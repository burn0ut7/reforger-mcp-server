using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ReforgerMcp;

public sealed partial class WorkbenchSession
{
    internal volatile object AutomaticFocusPolicy = new { state = "not_started" };

    public Task<object> FocusPolicy(string action, int processId, CancellationToken ct)
    {
        if (action is not ("status" or "enable" or "disable"))
            throw new ArgumentException("action must be status, enable or disable.");
        return Run(_ =>
        {
            using var process = SelectProcess();
            if (process.Id != processId) throw new InvalidOperationException("Workbench process changed; inspect workbench_script_status.");
            return AutomaticFocusPolicy = NativeWorkbench.FocusPolicy(process, action);
        }, ct);
    }
}

internal sealed class WorkbenchFocusMonitor(Configuration configuration, WorkbenchSession session,
    ILogger<WorkbenchFocusMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (configuration.Workbench is null) return;
        (int Pid, long Started)? attempted = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var process = WorkbenchSession.SelectProcess(configuration);
                var identity = (process.Id, process.StartTime.ToUniversalTime().Ticks);
                if (attempted != identity && process.MainWindowHandle != 0)
                {
                    try
                    {
                        session.AutomaticFocusPolicy = NativeWorkbench.FocusPolicy(process, "ensure");
                        attempted = identity;
                        logger.LogInformation("Workbench background focus policy attached to process {ProcessId}", process.Id);
                    }
                    catch (WorkbenchBusyException) { /* Module loading / concurrent attach: defer. */ }
                    catch (Exception ex)
                    {
                        attempted = identity;
                        session.AutomaticFocusPolicy = new { state = "unavailable", processId = process.Id, reason = ex.Message };
                        logger.LogWarning("Workbench background focus policy unavailable: {Reason}", ex.Message);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A pinned PID may have exited (ArgumentException), or inspection
                // may race process exit/access changes (Win32Exception). Discovery
                // must never take the shared MCP host down.
                attempted = null;
                session.AutomaticFocusPolicy = new { state = "waiting_for_workbench", reason = ex.Message };
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
