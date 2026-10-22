using System.Diagnostics;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ModelContextProtocol;

namespace ReforgerMcp;

// Owns all execution of the one pinned analysis engine, including process-tree cancellation.
public sealed class GhidraProcess(Configuration config)
{
    public string BridgePath => Path.Combine(AppContext.BaseDirectory, "Ghidra", "ReforgerBridge.java");
    public void CheckRuntime()
    {
        var properties = Path.Combine(config.GhidraHome, "Ghidra", "application.properties");
        if (!File.Exists(properties) || !File.ReadLines(properties).Contains("application.version=" + Configuration.GhidraVersion))
            throw new McpException("Configure the required Ghidra " + Configuration.GhidraVersion + " distribution; no substitute backend is used.");
        if (!File.Exists(Path.Combine(config.JavaHome, "bin", "java.exe")) || !File.Exists(Path.Combine(config.JavaHome, "bin", "javac.exe")))
            throw new McpException("javaHome must contain a JDK with bin/java.exe and bin/javac.exe.");
        var release = Path.Combine(config.JavaHome, "release");
        if (!File.Exists(release) || !File.ReadLines(release).Any(l => l.StartsWith("JAVA_VERSION=\"21.")))
            throw new McpException("This pinned integration requires JDK 21.");
        if (!File.Exists(BridgePath)) throw new McpException("Packaged Ghidra/ReforgerBridge.java is missing.");
    }

    public async Task<JsonElement> RunAsync(string directory, string projectDirectory, string? import,
        object request, Action<string>? progress, CancellationToken cancellationToken)
    {
        CheckRuntime();
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(projectDirectory);
        var requestPath = Path.Combine(directory, "request.json");
        var responsePath = Path.Combine(directory, "response.json");
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, Configuration.Json), cancellationToken);
        var start = new ProcessStartInfo(Path.Combine(config.JavaHome, "bin", "java.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        string[] arguments = [ $"-Xmx{config.JavaHeapMegabytes}M", "-XX:ParallelGCThreads=2", "-XX:CICompilerCount=2",
            "-Djava.system.class.loader=ghidra.GhidraClassLoader", "-Dfile.encoding=UTF8", "-Duser.language=en", "-Duser.country=US",
            "-cp", Path.Combine(config.GhidraHome, "Ghidra", "Framework", "Utility", "lib", "Utility.jar"),
            "ghidra.Ghidra", "ghidra.app.util.headless.AnalyzeHeadless", projectDirectory, "Engine" ];
        foreach (var a in arguments) start.ArgumentList.Add(a);
        if (import is null)
            foreach (var a in new[] { "-process", "Engine.exe", "-readOnly", "-noanalysis" }) start.ArgumentList.Add(a);
        else
            foreach (var a in new[] { "-import", import, "-analysisTimeoutPerFile", config.AnalysisTimeoutSeconds.ToString(), "-max-cpu", config.AnalysisCpuCount.ToString() }) start.ArgumentList.Add(a);
        foreach (var a in new[] { "-scriptPath", Path.GetDirectoryName(BridgePath)!, "-postScript", "ReforgerBridge.java", requestPath, responsePath,
            "-log", Path.Combine(directory, "analysis.log") }) start.ArgumentList.Add(a);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(import is null ? config.QueryTimeoutSeconds : config.AnalysisTimeoutSeconds + 180));
        using var process = new Process { StartInfo = start };
        using var processFamily = ProcessFamily.Create();
        using var log = new StreamWriter(Path.Combine(directory, "console.log")) { AutoFlush = true };
        var logGate = new SemaphoreSlim(1);
        async Task Drain(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                await logGate.WaitAsync();
                try { await log.WriteLineAsync(line); progress?.Invoke(line.Length > 500 ? line[..500] : line); }
                finally { logGate.Release(); }
            }
        }
        process.Start();
        try { ProcessFamily.Assign(processFamily,process); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree:true); throw; }
        var timer = Stopwatch.StartNew();
        void Record(string state) => File.WriteAllText(Path.Combine(directory,"process.json"),JsonSerializer.Serialize(
            new { state, processId = process.Id, elapsedSeconds = timer.Elapsed.TotalSeconds, exitCode = process.ExitCode },Configuration.Json));
        var drains = Task.WhenAll(Drain(process.StandardOutput), Drain(process.StandardError));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            await drains;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await drains;
            Record(cancellationToken.IsCancellationRequested ? "cancelled" : "timed_out");
            if (cancellationToken.IsCancellationRequested) throw;
            throw new McpException("Ghidra exceeded the configured deadline. No retry was performed. Evidence: " + directory);
        }
        Record("exited");
        if (process.ExitCode != 0 || !File.Exists(responsePath))
            throw new McpException($"Ghidra failed (exit {process.ExitCode}) or did not return a result. Inspect {directory}");
        if (new FileInfo(responsePath).Length > 1_000_000) throw new McpException("Ghidra response exceeds the result-size bound. Inspect " + directory);
        using var response = JsonDocument.Parse(await File.ReadAllTextAsync(responsePath, cancellationToken));
        if (!response.RootElement.GetProperty("ok").GetBoolean())
            throw new McpException(response.RootElement.GetProperty("error").GetString() + ". Evidence: " + directory);
        return response.RootElement.Clone();
    }

    // Windows closes this job handle even if the MCP host crashes, terminating its Java/native children.
    private static class ProcessFamily
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long ProcessTime, JobTime;
            public uint Flags;
            public nuint MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcesses;
            public nuint Affinity;
            public uint Priority, Scheduling;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public IoCounters Io;
            public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObjectW(nint attributes, string? name);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimits info, int size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, nint process);
        public static SafeFileHandle Create()
        {
            var job = CreateJobObjectW(0,null);
            if (job.IsInvalid) { job.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
            var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } }; // KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(job,9,ref limits,Marshal.SizeOf<ExtendedLimits>()))
            { var error = new Win32Exception(Marshal.GetLastWin32Error()); job.Dispose(); throw error; }
            return job;
        }
        public static void Assign(SafeFileHandle job, Process process)
        {
            if (!AssignProcessToJobObject(job,process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
