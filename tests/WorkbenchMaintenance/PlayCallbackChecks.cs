using System.Runtime.InteropServices;
using ReforgerMcp;

internal static class PlayCallbackChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Find(nint app, nint name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Instance(nint descriptor, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Flag(nint owner);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Dirty(nint owner, int mask);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Toggle(nint editor);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(nint output);

    internal static void Run()
    {
        foreach (var profile in new[] { NativeWorkbench.MaintenanceProfile.Legacy, NativeWorkbench.MaintenanceProfile.Current })
        {
            var image = Win.VirtualAlloc(0, 0x3000000, 0x3000, 0x40);
            if (image == 0) throw new Exception("Cannot allocate synthetic Play image");
            var allocations = new List<nint>();
            nint Alloc(int size) { var p = Marshal.AllocHGlobal(size); Marshal.Copy(new byte[size], 0, p, size); allocations.Add(p); return p; }
            var output = Alloc(256); var descriptor = Alloc(64); var descriptorTable = Alloc(256);
            var editor = Alloc(512); var apiTable = Alloc(256); var document = Alloc(128);
            var path = Marshal.StringToCoTaskMemUTF8("Fixture.ent");
            var roots = new List<Delegate>();
            nint Root(Delegate d) { roots.Add(d); return Marshal.GetFunctionPointerForDelegate(d); }
            void Hook(int rva, Delegate d)
            {
                var bytes = new List<byte> { 0x48, 0xb8 }; bytes.AddRange(BitConverter.GetBytes((long)Root(d))); bytes.AddRange([0xff, 0xe0]);
                Marshal.Copy(bytes.ToArray(), 0, image + rva, bytes.Count);
            }
            bool playing = false, dirty = false, missing = false; int toggles = 0;
            void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
            try
            {
                Marshal.WriteIntPtr(descriptor, descriptorTable); Marshal.WriteInt32(descriptor, 0x24, 1);
                Marshal.WriteIntPtr(editor, image + profile.EditorVtable); Marshal.WriteIntPtr(editor, 0xa0, apiTable);
                Marshal.WriteIntPtr(editor, 0x120, document); Marshal.WriteIntPtr(document, 0x50, path);
                Marshal.WriteIntPtr(descriptorTable, 0xd8, Root((Instance)((owner, index) => { Require(owner == descriptor && index == 0, "instance ABI"); return editor; })));
                Marshal.WriteIntPtr(apiTable, 0x98, Root((Flag)(owner => { Require(owner == editor + 0xa0, "Play state receiver"); return playing ? (byte)1 : (byte)0; })));
                Marshal.WriteIntPtr(apiTable, 0x70, Root((Dirty)((owner, mask) => { Require(owner == editor + 0xa0 && mask == 0x1f, "dirty ABI"); return dirty ? (byte)1 : (byte)0; })));
                Hook(profile.EditorGetter, (Find)((_, name) => { Require(name == image + profile.EditorName, "descriptor name"); return missing ? 0 : descriptor; }));
                Hook(profile.PlayAction, (Toggle)(owner => { Require(owner == editor, "Play owner"); playing = !playing; toggles++; }));
                void Run(string action)
                {
                    Marshal.Copy(new byte[256], 0, output, 256);
                    var code = NativeWorkbench.PlayProbe(image, action, profile);
                    Marshal.Copy(code, 0, image, code.Length);
                    Require(Win.FlushInstructionCache(Win.GetCurrentProcess(), image, 0x3000000), "flush");
                    Marshal.GetDelegateForFunctionPointer<Probe>(image)(output);
                }
                Run("status"); Require(toggles == 0 && Marshal.ReadInt32(output, 112) == 1, "status does not toggle");
                Run("start"); Require(playing && toggles == 1 && Marshal.ReadInt32(output, 108) == 1, "start observes transition");
                Run("start"); Require(toggles == 1, "repeat start is idempotent");
                dirty = true; Run("stop"); Require(!playing && toggles == 2, "stop preserves lifecycle despite dirty flag");
                Run("start"); Require(!playing && toggles == 2 && Marshal.ReadInt32(output, 72) == 5, "dirty start refuses");
                dirty = false; Marshal.WriteIntPtr(document, 0x50, 0); Run("start"); Require(toggles == 2 && Marshal.ReadInt32(output, 72) == 7, "unnamed start refuses");
                missing = true; Run("status"); Require(Marshal.ReadInt32(output, 72) == 7, "missing editor refuses");
                missing = false; Marshal.WriteIntPtr(editor, 0); Run("status"); Require(Marshal.ReadInt32(output, 72) == 4, "wrong vtable refuses");
                Console.WriteLine("PASS " + (profile.Limited ? "1.9" : "legacy") + " Play callback ABI and state/dirty/missing/vtable guards");
            }
            finally { GC.KeepAlive(roots); Marshal.FreeCoTaskMem(path); foreach (var p in allocations) Marshal.FreeHGlobal(p); Win.VirtualFree(image, 0, 0x8000); }
        }
    }
}
