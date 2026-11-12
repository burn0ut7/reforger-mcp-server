using System.Runtime.InteropServices;
using ReforgerMcp;

internal static class ExtensionCallbackChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Load(nint path, nint reserved, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Find(nint module, nint name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Start(nint specification);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Error();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(nint data);

    internal static void Run()
    {
        var code = Win.VirtualAlloc(0, 4096, 0x3000, 0x40);
        var data = Marshal.AllocHGlobal(4096);
        var delegates = new List<Delegate>();
        nint Root(Delegate callback) { delegates.Add(callback); return Marshal.GetFunctionPointerForDelegate(callback); }
        void Require(bool okay, string detail) { if (!okay) throw new Exception("Extension callback: " + detail); }
        var command = new NativeWorkbench.ExtensionCommand("C:\\Fixture ü\\rstauthoring.dll", "C:\\Evidence\\drag.jsonl");
        bool failLoad = false, failFind = false;
        int loads = 0, finds = 0, starts = 0;
        try
        {
            Require(code != 0, "allocation");
            var entry = Root((Start)(specification => {
                Require(Marshal.PtrToStringUni(specification) == command.Specification, "UTF-16 specification and offset");
                starts++;
                return -1;
            }));
            var load = Root((Load)((path, reserved, flags) => {
                Require(Marshal.PtrToStringUni(path) == command.FilePath && reserved == 0 && flags == 0xb00, "exact UTF-16 path and restricted loader flags");
                loads++;
                return failLoad ? 0 : 0x123456;
            }));
            var find = Root((Find)((module, name) => {
                Require(module == 0x123456 && Marshal.PtrToStringUTF8(name) == "RST_WorkbenchExtensionStart_v1", "module and versioned entry name");
                finds++;
                return failFind ? 0 : entry;
            }));
            var error = Root((Error)(() => 127));
            var emitted = NativeWorkbench.ExtensionProbe(load, find, error);
            Marshal.Copy(emitted, 0, code, emitted.Length);
            Require(Win.FlushInstructionCache(Win.GetCurrentProcess(), code, 4096), "instruction cache");
            var run = Marshal.GetDelegateForFunctionPointer<Probe>(code);
            void Invoke()
            {
                Marshal.Copy(new byte[4096], 0, data, 4096);
                var input = NativeWorkbench.ExtensionInput(command);
                Marshal.Copy(input, 0, data + 512, input.Length);
                loads = finds = starts = 0;
                run(data);
            }
            Invoke();
            Require(loads == 1 && finds == 1 && starts == 1, "one dispatch, no replay");
            Require(Marshal.ReadInt32(data, 68) == 1 && Marshal.ReadInt32(data, 104) == 2 && Marshal.ReadInt32(data, 108) == -1,
                "negative extension return is preserved, not treated as functional success");
            Require(Marshal.ReadInt64(data, 112) == 0x123456, "resident module reported");
            failFind = true; Invoke();
            Require(loads == 1 && finds == 1 && starts == 0 && Marshal.ReadInt32(data, 104) == 1 && Marshal.ReadInt32(data, 108) == 127,
                "missing entry retains module and reports error without invocation");
            failLoad = true; Invoke();
            Require(loads == 1 && finds == 0 && starts == 0 && Marshal.ReadInt32(data, 104) == 0 && Marshal.ReadInt32(data, 108) == 127,
                "load failure stops before export lookup");
            foreach (var invalid in new[] { new string('x', 768), "x\0y" })
            {
                bool rejected = false;
                try { NativeWorkbench.ExtensionInput(command with { Specification = invalid }); } catch (ArgumentException) { rejected = true; }
                Require(rejected, "input bound/NUL rejection");
            }
            Console.WriteLine("PASS extension loader ABI, exact path, failed loads/entries, return codes and input bounds");
        }
        finally
        {
            GC.KeepAlive(delegates);
            Marshal.FreeHGlobal(data);
            if (code != 0) Win.VirtualFree(code, 0, 0x8000);
        }
    }
}
