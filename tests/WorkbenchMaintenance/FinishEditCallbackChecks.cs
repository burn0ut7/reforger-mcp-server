using System.Runtime.InteropServices;
using ReforgerMcp;

internal static class FinishEditCallbackChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(nint output);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Object(nint accessible);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Cast(nint meta, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Check(nint edit);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Enabled(nint edit, nint ancestor);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Finish(nint edit);

    internal static void Run()
    {
        var image = Win.VirtualAlloc(0, 0x500000, 0x3000, 0x40);
        if (image == 0) throw new Exception("Cannot allocate finish-edit fixture");
        var allocations = new List<nint>(); var roots = new List<Delegate>();
        nint Alloc(int size) { var p = Marshal.AllocHGlobal(size); Marshal.Copy(new byte[size], 0, p, size); allocations.Add(p); return p; }
        nint Callback(Delegate callback) { roots.Add(callback); return Marshal.GetFunctionPointerForDelegate(callback); }
        void Hook(int rva, Delegate callback)
        {
            var bytes = new List<byte> { 0x48, 0xb8 };
            bytes.AddRange(BitConverter.GetBytes((long)Callback(callback))); bytes.AddRange([0xff, 0xe0]);
            Marshal.Copy(bytes.ToArray(), 0, image + rva, bytes.Count);
        }
        void Require(bool condition, string name) { if (!condition) throw new Exception(name); }
        var output = Alloc(512); var accessible = Alloc(8); var vtable = Alloc(24); var edit = Alloc(8);
        bool exists = true, correctType = true, readOnly = false, enabled = true, acceptable = true;
        int casts = 0, checks = 0, commits = 0;
        try
        {
            Marshal.WriteIntPtr(output, 96, accessible); Marshal.WriteIntPtr(accessible, vtable);
            Marshal.WriteIntPtr(vtable, 16, Callback((Object)(owner => { Require(owner == accessible, "Accessible object ABI"); return exists ? edit : 0; })));
            Hook(0x38f0, (Cast)((meta, value) => { Require(meta == image + 0x48fd40 && value == edit, "QLineEdit cast ABI"); casts++; return correctType ? edit : 0; }));
            Hook(0x185c40, (Check)(owner => { Require(owner == edit, "Read-only owner"); checks++; return (byte)(readOnly ? 1 : 0); }));
            Hook(0x5c820, (Enabled)((owner, ancestor) => { Require(owner == edit && ancestor == 0, "Enabled owner and ancestor"); checks++; return (byte)(enabled ? 1 : 0); }));
            Hook(0x185490, (Check)(owner => { Require(owner == edit, "Input validator owner"); checks++; return (byte)(acceptable ? 1 : 0); }));
            Hook(0x184d90, (Finish)(owner => { Require(owner == edit, "Finish owner"); commits++; }));
            var code = NativeWorkbench.FinishEditProbe(image, image);
            Marshal.Copy(code, 0, image, code.Length);
            Require(Win.FlushInstructionCache(Win.GetCurrentProcess(), image, 0x500000), "Flush finish-edit fixture");
            var run = Marshal.GetDelegateForFunctionPointer<Probe>(image);
            void Verify(int expectedCasts, int expectedChecks, int expectedCommits, string name)
            {
                casts = checks = commits = 0; Marshal.WriteInt32(output, 68, 0); run(output);
                Require(casts == expectedCasts && checks == expectedChecks && commits == expectedCommits && Marshal.ReadInt32(output, 68) == expectedCommits, name);
            }
            Verify(1, 3, 1, "Valid editable input finishes exactly once");
            exists = false; Verify(0, 0, 0, "Missing object refuses before cast"); exists = true;
            correctType = false; Verify(1, 0, 0, "Wrong type refuses before widget calls"); correctType = true;
            readOnly = true; Verify(1, 1, 0, "Read-only editor refuses"); readOnly = false;
            enabled = false; Verify(1, 2, 0, "Disabled editor refuses"); enabled = true;
            acceptable = false; Verify(1, 3, 0, "Invalid input refuses");
            Console.WriteLine("PASS finish-edit callback type, input guards and signal ABI");
        }
        finally
        {
            GC.KeepAlive(roots);
            foreach (var allocation in allocations) Marshal.FreeHGlobal(allocation);
            Win.VirtualFree(image, 0, 0x8000);
        }
    }
}
