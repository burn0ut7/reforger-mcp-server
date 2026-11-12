using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ReforgerMcp;

// Invoked directly by the installed Workbench profile plugin. It can service only
// that parent process, never discover/watch future Workbench launches.
internal static class WorkbenchAuthoringStartup
{
    internal static void RecordVerifiedLoad(string control, int processId, long startedTicks, string dll, string sha256)
    {
        var directory = Path.Combine(control, "attempts");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, processId + "-" + startedTicks + ".verified"),
            JsonSerializer.Serialize(new VerifiedLoad(Path.GetFullPath(dll), sha256)));
    }

    internal static bool HasVerifiedLoad(string control, int processId, long startedTicks, string dll, string sha256)
    {
        try
        {
            var record = JsonSerializer.Deserialize<VerifiedLoad>(File.ReadAllText(
                Path.Combine(control, "attempts", processId + "-" + startedTicks + ".verified")));
            return record is not null && string.Equals(record.Path, Path.GetFullPath(dll), StringComparison.OrdinalIgnoreCase)
                && string.Equals(record.Sha256, sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (JsonException) { return false; }
    }

    private sealed record VerifiedLoad(string Path, string Sha256);

    internal static string ClaimLoadAttempt(string controlDirectory, int processId, long startedTicks)
    {
        var directory = Path.Combine(controlDirectory, "attempts");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, processId + "-" + startedTicks);
        using var marker = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        return path;
    }

    internal static FileStream? AcquireLease(string path)
    {
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read); }
        catch (IOException) { return null; }
    }

    internal static bool IsActive(string control, string configurationPath)
    {
        try
        {
            return string.Equals(JsonSerializer.Deserialize<string>(File.ReadAllText(Path.Combine(control, "active.json"))),
                Path.GetFullPath(configurationPath), StringComparison.OrdinalIgnoreCase);
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    internal static async Task Run(string configurationPath, string dllPath, CancellationToken ct)
    {
        var configuration = Configuration.Load(configurationPath);
        var workbench = configuration.Workbench ?? throw new InvalidOperationException("Workbench is not configured.");
        if (configuration.LocalHttp is not null)
            throw new InvalidOperationException("Workbench startup requires the installed standalone helper configuration.");
        using var self = Process.GetCurrentProcess();
        if (NtQueryInformationProcess(self.Handle, 0, out var info, Marshal.SizeOf<ProcessBasicInformation>(), out _) != 0)
            throw new InvalidOperationException("Cannot identify the launching Workbench process.");
        using var parent = Process.GetProcessById(checked((int)info.ParentProcessId));
        var started = parent.StartTime.ToUniversalTime();
        if (parent.HasExited || started > self.StartTime.ToUniversalTime() ||
            !string.Equals(parent.MainModule?.FileName, workbench.Executable, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Startup must be launched directly by the configured Workbench executable.");
        var control = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GCWorkbench");
        if (!IsActive(control, configurationPath)) return;
        var identity = parent.Id + "-" + started.Ticks;
        var folder = Path.Combine(configuration.DataDirectory, "startup", identity);
        Directory.CreateDirectory(folder);
        using var lease = AcquireLease(Path.Combine(control, identity + ".lock"));
        if (lease is null) return;
        using var log = new StreamWriter(Path.Combine(folder, "startup.log"), append: true) { AutoFlush = true };
        Console.SetOut(log);
        Console.SetError(log);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lifetime.CancelAfter(TimeSpan.FromMinutes(2));
        var pinned = configuration with
        {
            DataDirectory = folder,
            Workbench = workbench with { ProcessId = parent.Id, ProcessStartedUtc = started }
        };
        var path = Path.Combine(folder, "configuration.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(pinned, Configuration.Json), lifetime.Token);
        var observation = ObserveLifetime();
        try
        {
            Console.WriteLine($"{DateTimeOffset.Now:o} Starting for Workbench {identity}.");
            while (!lifetime.IsCancellationRequested)
            {
                if (await WorkbenchAuthoringSetup.Run(pinned, path, dllPath, lifetime.Token, automatic: true))
                {
                    Console.WriteLine($"{DateTimeOffset.Now:o} Loaded or already resident; helper exiting.");
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(2), lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            Console.WriteLine($"{DateTimeOffset.Now:o} Host closed, installation removed, or two-minute readiness deadline reached; helper exiting.");
        }
        catch (Exception ex)
        {
            // No replay after an uncertain mutation; the shared process gate survives reload/upgrades.
            Console.Error.WriteLine($"{DateTimeOffset.Now:o} {ex.Message}");
        }
        finally
        {
            lifetime.Cancel();
            await observation;
            // Program's error reporting must not retain the disposed writer.
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }

        async Task ObserveLifetime()
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    if (parent.HasExited || !IsActive(control, configurationPath)) { lifetime.Cancel(); return; }
                    await Task.Delay(250, lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        internal nint Reserved1, PebBaseAddress, Reserved2, Reserved3, ProcessId, ParentProcessId;
    }
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(nint process, int informationClass,
        out ProcessBasicInformation information, int length, out int returnedLength);
}
