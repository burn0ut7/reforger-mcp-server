using System.Runtime.InteropServices;
using ReforgerMcp;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

var data = Marshal.AllocHGlobal(4096);
Marshal.Copy(new byte[4096], 0, data, 4096);
var code = Win.VirtualAlloc(0, 4096, 0x3000, 0x40);
if (code == 0) throw new Exception("VirtualAlloc failed");
uint foregroundPid = 999;
var calls = 0;
long[] observed = [];
GetForeground foreground = () => (nint)123;
GetPid pid = (nint hwnd, out uint value) => { value = foregroundPid; return 42; };
One one = value => { calls++; observed = [value]; return 77; };
Two two = (a, b) => { calls++; observed = [a, b]; return 77; };
Seven seven = (a, b, c, d, e, f, g) => { calls++; observed = [a, b, c, d, e, f, g]; return 77; };
Two placement = (a, b) => { calls++; observed = [a, Marshal.ReadInt32(b, 8), Marshal.ReadInt32(b, 40)]; return 77; };
var count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
T Stub<T>(int index, Delegate original) where T : Delegate
{
    var bytes = NativeWorkbench.FocusStub(index, Marshal.GetFunctionPointerForDelegate(original), data,
        Marshal.GetFunctionPointerForDelegate(foreground), Marshal.GetFunctionPointerForDelegate(pid), 100);
    Marshal.Copy(bytes, 0, code, bytes.Length);
    if (!Win.FlushInstructionCache(Win.GetCurrentProcess(), code, (nuint)bytes.Length)) throw new Exception("Flush failed");
    calls = 0;
    return Marshal.GetDelegateForFunctionPointer<T>(code);
}
try
{
    for (var i = 0; i < 4; i++)
    {
        var hook = Stub<One>(i, one);
        Marshal.WriteByte(data, 8, 1); foregroundPid = 999;
        Check(hook(456) == (i == 3 ? 1 : 0) && calls == 0, "background activation blocked " + i);
        Check(Marshal.ReadInt64(data, 16 + i * 8) == 1, "counter " + i);
        foregroundPid = 100;
        Check(hook(456) == 77 && calls == 1 && observed[0] == 456, "foreground activation passed " + i);
        Marshal.WriteByte(data, 8, 0); foregroundPid = 999;
        Check(hook(789) == 77 && calls == 2 && observed[0] == 789, "disabled activation passed " + i);
    }
    Marshal.WriteByte(data, 8, 1); foregroundPid = 999;
    var show = Stub<Two>(6, two);
    foreach (var command in new[] { 1, 3, 5, 9, 10 })
        Check(show(456, command) == 77 && observed.SequenceEqual(new long[] { 456, 4 }), "nonactivating show " + command);
    foreach (var command in new[] { 2, 6, 11 })
        Check(show(456, command) == 77 && observed[1] == 7, "nonactivating minimize " + command);
    foreach (var command in new[] { 0, 4, 7, 8 })
        Check(show(456, command) == 77 && observed[1] == command, "preserve show " + command);
    var position = Stub<Seven>(7, seven);
    Check(position(1, 2, 3, 4, 5, 6, 0x40) == 77 && observed.SequenceEqual(new long[] { 1, 2, 3, 4, 5, 6, 0x54 }), "position geometry preserved, no activation/z-order");
    foregroundPid = 100;
    Check(position(1, 2, 3, 4, 5, 6, 0x40) == 77 && observed[6] == 0x40, "foreground position unchanged");
    var buffer = Marshal.AllocHGlobal(48);
    try
    {
        Marshal.Copy(new byte[48], 0, buffer, 48);
        Marshal.WriteInt32(buffer, 0, 44); Marshal.WriteInt32(buffer, 8, 3); Marshal.WriteInt32(buffer, 40, 1234);
        foregroundPid = 999;
        var place = Stub<Two>(4, placement);
        Check(place(456, buffer) == 77 && observed.SequenceEqual(new long[] { 456, 4, 1234 }), "placement copied with nonactivating show and intact rectangle");
        Check(Marshal.ReadInt32(buffer, 8) == 3, "caller placement untouched");
        var flash = Stub<One>(5, one);
        Marshal.WriteInt32(buffer, 16, 2);
        Check(flash(buffer) == 0 && calls == 0, "background flash suppressed");
        foregroundPid = 100;
        Check(flash(buffer) == 0 && calls == 0, "foreground flash suppressed");
        Marshal.WriteInt32(buffer, 16, 0);
        Check(flash(buffer) == 77 && calls == 1, "flash stop passed");
        Marshal.WriteByte(data, 8, 0); Marshal.WriteInt32(buffer, 16, 2);
        Check(flash(buffer) == 77 && calls == 2, "disabled flash passed");
    }
    finally { Marshal.FreeHGlobal(buffer); }
    Console.WriteLine($"Passed {count} native stub checks.");
}
finally
{
    GC.KeepAlive(foreground); GC.KeepAlive(pid); GC.KeepAlive(one); GC.KeepAlive(two); GC.KeepAlive(seven); GC.KeepAlive(placement);
    Win.VirtualFree(code, 0, 0x8000); Marshal.FreeHGlobal(data);
}
var config = new Configuration
{
    DataDirectory = Path.GetTempPath(),
    Workbench = new WorkbenchConfiguration
    {
        Executable = "C:/missing-workbench.exe", ProcessId = int.MaxValue, ProcessStartedUtc = DateTimeOffset.UtcNow
    }
};
var session = new WorkbenchSession(config, new WorkbenchWindows(config), new ConsoleLogs(config));
using (var monitor = new WorkbenchFocusMonitor(config, session, NullLogger<WorkbenchFocusMonitor>.Instance))
{
    await monitor.StartAsync(CancellationToken.None);
    for (var attempt = 0; attempt < 100 && JsonSerializer.Serialize(session.AutomaticFocusPolicy).Contains("not_started"); attempt++)
        await Task.Delay(20);
    Check(monitor.ExecuteTask is { IsCompleted: false }, "missing pinned process does not stop focus monitor");
    Check(JsonSerializer.Serialize(session.AutomaticFocusPolicy).Contains("waiting_for_workbench"), "missing pinned process reported");
    await monitor.StopAsync(CancellationToken.None);
}
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint GetForeground();
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate uint GetPid(nint hwnd, out uint pid);
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint One(nint a);
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint Two(nint a, nint b);
[UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate nint Seven(nint a, nint b, int c, int d, int e, int f, uint g);
static class Win
{
    [DllImport("kernel32")] internal static extern nint VirtualAlloc(nint address, nuint size, uint type, uint protection);
    [DllImport("kernel32")] internal static extern bool VirtualFree(nint address, nuint size, uint type);
    [DllImport("kernel32")] internal static extern bool FlushInstructionCache(nint process, nint address, nuint size);
    [DllImport("kernel32")] internal static extern nint GetCurrentProcess();
}
