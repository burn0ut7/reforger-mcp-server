using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ReforgerMcp;

// Observations come from the process handle owned by the loaded Workbench plugin,
// not process-name matching or assumptions about which instance is newest.
internal static class MultiplayerProcess
{
    internal sealed record Observation(string State, int? ProcessId = null, DateTime? StartedUtc = null,
        bool? Running = null, uint? ExitCode = null, string? Executable = null, string[]? Arguments = null,
        string? LogDirectory = null, string[]? CandidateSessions = null, string? Detail = null);

    internal static Observation Observe(nint owner, nint handle)
    {
        if (handle == 0) return new("not_started");
        if (!DuplicateHandle(owner, handle, GetCurrentProcess(), out var local, 0, false, 2))
        {
            var error = Marshal.GetLastWin32Error();
            return new(error == 6 ? "closed_handle" : "handle_unavailable", Detail: new Win32Exception(error).Message + " No process is identified by this handle; this alone does not prove a previous process exited.");
        }
        try
        {
            var pid = GetProcessId(local);
            if (pid == 0)
            {
                var identityError = Marshal.GetLastWin32Error();
                return new(identityError == 6 ? "closed_handle" : "unavailable", Detail: "Stored handle no longer identifies a process: " + new Win32Exception(identityError).Message);
            }
            if (!GetProcessTimes(local, out var created, out _, out _, out _) || !GetExitCodeProcess(local, out var exit))
                return new("unavailable", Detail: new Win32Exception(Marshal.GetLastWin32Error()).Message);
            var started = DateTime.FromFileTimeUtc(created);
            var path = new StringBuilder(32768); var length = path.Capacity;
            var executable = QueryFullProcessImageName(local, 0, path, ref length) ? path.ToString() : null;
            if (exit != 259) return new("exited", (int)pid, started, false, exit, executable);
            var (args, error) = Arguments(local);
            string? Option(string key)
            {
                string? value = null;
                for (var i = 0; i < args.Length; i++)
                    if (args[i].Equals("-" + key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) value = args[++i];
                    else if (args[i].StartsWith("-" + key + "=", StringComparison.OrdinalIgnoreCase)) value = args[i][(key.Length + 2)..];
                return value;
            }
            string? root = null;
            if (error is null)
            {
                var profile = Option("profile") ?? "ArmaReforger";
                root = Option("logsDir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", profile, "logs");
                if (!Path.IsPathFullyQualified(root)) root = null;
            }
            var sessions = root is not null && Directory.Exists(root)
                ? Directory.EnumerateDirectories(root, "logs_*").Where(p => Math.Abs((Directory.GetCreationTimeUtc(p) - started).TotalSeconds) <= 15)
                    .Where(p => File.Exists(Path.Combine(p, "console.log"))).Select(Path.GetFileName).OfType<string>().Take(8).ToArray() : [];
            return new("running", (int)pid, started, true, null, executable, Redact(args), root, sessions,
                error ?? "Log directory inferred from this process's command line; candidate sessions were created within 15 seconds of process creation. Confirm their startup header and connection events; they are not proven process-to-log bindings.");
        }
        finally { CloseHandle(local); }
    }

    private static (string[], string?) Arguments(nint process)
    {
        const int capacity = 131072;
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            var status = NtQueryInformationProcess(process, 60, buffer, capacity, out _);
            if (status != 0) return ([], "Command line unavailable: NTSTATUS 0x" + status.ToString("x8"));
            var length = (ushort)Marshal.ReadInt16(buffer);
            var text = Marshal.ReadIntPtr(buffer, 8);
            if ((length & 1) != 0 || text < buffer || text + length > buffer + capacity) return ([], "Invalid command-line response.");
            var command = Marshal.PtrToStringUni(text, length / 2)!;
            var argv = CommandLineToArgvW(command, out var count);
            if (argv == 0) return ([], "Command-line parsing unavailable.");
            try { return (Enumerable.Range(0, count).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size))!).ToArray(), null); }
            finally { LocalFree(argv); }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal static string[] Redact(string[] args)
    {
        var result = (string[])args.Clone();
        for (var i = 0; i < result.Length; i++)
        {
            var part = result[i];
            if (!part.StartsWith('-')) continue;
            var key = part.Split('=')[0];
            if (!key.Contains("password", StringComparison.OrdinalIgnoreCase) && !key.Contains("token", StringComparison.OrdinalIgnoreCase) && !key.Contains("secret", StringComparison.OrdinalIgnoreCase)) continue;
            var equals = part.IndexOf('=');
            if (equals >= 0) result[i] = part[..(equals + 1)] + "[redacted]";
            else if (i + 1 < result.Length) result[++i] = "[redacted]";
        }
        return result;
    }

    [DllImport("kernel32", SetLastError = true)] private static extern bool DuplicateHandle(nint source, nint handle, nint target, out nint copy, uint access, bool inherit, uint options);
    [DllImport("kernel32")] private static extern nint GetCurrentProcess();
    [DllImport("kernel32")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32", SetLastError = true)] private static extern uint GetProcessId(nint process);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetProcessTimes(nint process, out long created, out long exited, out long kernel, out long user);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetExitCodeProcess(nint process, out uint code);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref int length);
    [DllImport("ntdll")] private static extern int NtQueryInformationProcess(nint process, int infoClass, nint buffer, int length, out int returned);
    [DllImport("shell32", CharSet = CharSet.Unicode)] private static extern nint CommandLineToArgvW(string command, out int count);
    [DllImport("kernel32")] private static extern nint LocalFree(nint memory);
}

