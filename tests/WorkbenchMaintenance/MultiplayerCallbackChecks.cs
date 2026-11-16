using System.Runtime.InteropServices;
using System.Text;
using ReforgerMcp;

internal static class MultiplayerCallbackChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Find(nint app, nint name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Instance(nint descriptor, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Field(nint owner, nint output, nint name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Predicate(nint type);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Assign(nint value, nint type, nint storage, byte copy);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Action(nint owner);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(nint output);

    internal static void Run()
    {
        foreach (var profile in new[] { NativeWorkbench.MaintenanceProfile.Legacy, NativeWorkbench.MaintenanceProfile.Current })
        {
            var image = Win.VirtualAlloc(0, 0x3000000, 0x3000, 0x40);
            if (image == 0) throw new Exception("Cannot allocate multiplayer test image");
            var allocations = new List<nint>();
            var roots = new List<Delegate>();
            nint Alloc(int size) { var p = Marshal.AllocHGlobal(size); Marshal.Copy(new byte[size], 0, p, size); allocations.Add(p); return p; }
            nint Text(string value) { var bytes = Encoding.UTF8.GetBytes(value + "\0"); var p = Alloc(bytes.Length); Marshal.Copy(bytes, 0, p, bytes.Length); return p; }
            nint Root(Delegate d) { roots.Add(d); return Marshal.GetFunctionPointerForDelegate(d); }
            void Hook(int rva, Delegate d) { var b = new List<byte> { 0x48, 0xb8 }; b.AddRange(BitConverter.GetBytes((long)Root(d))); b.AddRange([0xff, 0xe0]); Marshal.Copy(b.ToArray(), 0, image + rva, b.Count); }
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
            var output = Alloc(4096); var descriptor = Alloc(64); var table = Alloc(256);
            var editor = Alloc(256); var menu = Alloc(128); var plugins = Alloc(8);
            var plugin = Alloc(32); var type = Alloc(32); var fieldType = Alloc(32); var storage = Alloc(8);
            var mp = profile.Multiplayer;
            int runs = 0, stops = 0, writes = 0; bool missingField = false, stringType = true;
            try
            {
                Marshal.WriteIntPtr(descriptor, table); Marshal.WriteInt32(descriptor, 0x24, 1);
                Marshal.WriteIntPtr(table, 0xd8, Root((Instance)((d, i) => { Check(d == descriptor && i == 0, "plugin instance ABI"); return editor; })));
                Marshal.WriteIntPtr(editor, image + profile.EditorVtable); Marshal.WriteIntPtr(editor, 0x78, menu);
                Marshal.WriteIntPtr(menu, image + mp.MenuVtable); Marshal.WriteIntPtr(menu, 0x60, plugins); Marshal.WriteInt32(menu, 0x6c, 1);
                Marshal.WriteIntPtr(plugins, plugin); Marshal.WriteIntPtr(plugin, 8, type); Marshal.WriteIntPtr(type, 16, Text("SCR_DedicatedServerPlugin"));
                Marshal.WriteIntPtr(storage, Text("initial profile")); Marshal.WriteInt32(fieldType, 16, 0x50000000);
                Hook(profile.EditorGetter, (Find)((_, name) => { Check(name == image + profile.EditorName, "plugin editor name"); return descriptor; }));
                Hook(mp.Field, (Field)((owner, result, name) => { Check(owner == plugin && Marshal.PtrToStringUTF8(name) == "m_sProfile", "reflection ABI"); Marshal.WriteIntPtr(result, missingField ? 0 : storage); Marshal.WriteIntPtr(result, 8, missingField ? 0 : fieldType); return result; }));
                Hook(mp.IsString, (Predicate)(t => { Check(t == fieldType, "string predicate ABI"); return stringType ? (byte)1 : (byte)0; }));
                Hook(mp.AssignString, (Assign)((value, t, target, copy) => { Check(t == fieldType && target == storage && copy == 1, "string assignment ABI"); Marshal.WriteIntPtr(storage, Text(Marshal.PtrToStringUTF8(value)!)); writes++; }));
                Hook(mp.Run, (Action)(owner => { Check(owner == plugin, "Run owner"); runs++; }));
                Hook(mp.Stop, (Action)(owner => { Check(owner == plugin, "Stop owner"); stops++; }));
                void Run(string action, string? field = null, string? value = null)
                {
                    Marshal.Copy(new byte[4096], 0, output, 4096);
                    var name = Encoding.UTF8.GetBytes("m_sProfile\0"); Marshal.Copy(name, 0, output + 512, name.Length);
                    if (value != null) { var b = Encoding.UTF8.GetBytes(value + "\0"); Marshal.Copy(b, 0, output + 1024, b.Length); }
                    var code = NativeWorkbench.MultiplayerProbe(image, new("dedicated", action, field, value), profile);
                    Marshal.Copy(code, 0, image, code.Length); Check(Win.FlushInstructionCache(Win.GetCurrentProcess(), image, 0x3000000), "flush");
                    Marshal.GetDelegateForFunctionPointer<Probe>(image)(output);
                }
                Run("status"); Check(Marshal.ReadInt32(output, 68) == 1 && runs == 0, "status without invocation");
                Run("get", "profile"); Check(Marshal.PtrToStringUTF8(output + 2048) == "initial profile", "profile read");
                Run("set", "profile", "changed profile"); Check(writes == 1 && Marshal.PtrToStringUTF8(output + 2048) == "changed profile", "profile assigned and read back");
                stringType = false; Run("set", "profile", "must not write"); Check(writes == 1 && Marshal.ReadInt32(output, 68) == 0, "wrong type rejected"); stringType = true;
                missingField = true; Run("get", "profile"); Check(Marshal.ReadInt32(output, 68) == 0, "missing field rejected"); missingField = false;
                Run("run"); Run("stop"); Check(runs == 1 && stops == 1, "lifecycle owner calls");
                Marshal.WriteIntPtr(menu, 0); Run("run"); Check(runs == 1 && Marshal.ReadInt32(output, 72) == 4, "wrong menu vtable rejected");
                Marshal.WriteIntPtr(menu, image + mp.MenuVtable); Marshal.WriteInt32(menu, 0x6c, 513); Run("run"); Check(runs == 1 && Marshal.ReadInt32(output, 72) == 4, "unbounded plugin count rejected");
                Console.WriteLine("PASS " + (profile.Limited ? "1.9" : "legacy") + " multiplayer lookup, reflection, string ownership, lifecycle and guards");
            }
            finally { GC.KeepAlive(roots); foreach (var p in allocations) Marshal.FreeHGlobal(p); Win.VirtualFree(image, 0, 0x8000); }
        }
    }
}
