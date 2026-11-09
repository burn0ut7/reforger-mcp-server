using System.Runtime.InteropServices;
using System.Text;
using ReforgerMcp;

internal static class WorldCallbackChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(nint output);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Find(nint app, nint name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Instance(nint descriptor, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Create(nint app, nint descriptor, int zero);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Flag(nint api);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Dirty(nint api, int mask);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Mask(nint api);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Construct(nint output, nint path);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Destroy(nint resource);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void PoolFree(nint pool, nint resource, int zero);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Owner();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Resolve(nint fs, nint path, nint output, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Exists(nint fs, nint path, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Open(nint editor, nint resource);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Free(nint allocation, int zero);

    internal static void Run()
    {
        foreach (var profile in new[] { NativeWorkbench.MaintenanceProfile.Legacy, NativeWorkbench.MaintenanceProfile.Current })
        {
            var image = Win.VirtualAlloc(0, 0x4000000, 0x3000, 0x40);
            if (image == 0) throw new Exception("Cannot allocate world fixture");
            var allocations = new List<nint>(); var roots = new List<Delegate>();
            nint Alloc(int bytes) { var p = Marshal.AllocHGlobal(bytes); Marshal.Copy(new byte[bytes], 0, p, bytes); allocations.Add(p); return p; }
            nint Text(string value, int refs = 1)
            {
                var bytes = Encoding.UTF8.GetBytes(value); var p = Alloc(bytes.Length + 13);
                Marshal.WriteInt32(p, 4, bytes.Length); Marshal.WriteInt32(p, 8, refs); Marshal.Copy(bytes, 0, p + 12, bytes.Length); return p + 12;
            }
            nint Root(Delegate callback) { roots.Add(callback); return Marshal.GetFunctionPointerForDelegate(callback); }
            void Hook(int rva, Delegate callback)
            {
                var code = new List<byte> { 0x48, 0xb8 }; code.AddRange(BitConverter.GetBytes((long)Root(callback))); code.AddRange([0xff, 0xe0]);
                Marshal.Copy(code.ToArray(), 0, image + rva, code.Count);
            }
            void Require(bool value, string label) { if (!value) throw new Exception(label); }
            var output = Alloc(4096); var app = Alloc(8); var descriptor = Alloc(64); var descriptorTable = Alloc(0xe0);
            var editor = Alloc(512); var apiTable = Alloc(256); var document = Alloc(128);
            var fs = Alloc(8); var fsTable = Alloc(0x158); var pool = Alloc(8); var poolTable = Alloc(32);
            bool playing = false, dirty = false, missing = false, exists = true, resolved = true;
            int opens = 0, creates = 0, destroys = 0, poolFrees = 0, stringFrees = 0, refs = 1;
            string resourcePath = "Fixture.ent", resolvedPath = "C:/Fixture/Fixture.ent";
            var resources = new List<nint>(); var strings = new List<nint>();
            try
            {
                Marshal.WriteIntPtr(descriptor, descriptorTable); Marshal.WriteInt32(descriptor, 0x24, 1);
                Marshal.WriteIntPtr(editor, image + profile.EditorVtable); Marshal.WriteIntPtr(editor, 0xa0, apiTable);
                Marshal.WriteIntPtr(editor, 0x120, document); Marshal.WriteIntPtr(document, 0x50, Text("Original.ent"));
                Marshal.WriteIntPtr(fs, fsTable); Marshal.WriteIntPtr(pool, poolTable); Marshal.WriteIntPtr(image + profile.ResourceNamePool, pool);
                Hook(profile.EditorGetter, (Find)((owner, name) => { Require(owner == app && name == image + profile.EditorName, "descriptor ABI"); return missing ? 0 : descriptor; }));
                Hook(profile.OpenEditor, (Create)((owner, type, zero) => { Require(owner == app && type == descriptor && zero == 0, "create editor ABI"); creates++; return editor; }));
                Marshal.WriteIntPtr(descriptorTable, 0xd8, Root((Instance)((owner, index) => { Require(owner == descriptor && index == 0, "instance ABI"); return editor; })));
                Marshal.WriteIntPtr(apiTable, 0x98, Root((Flag)(owner => { Require(owner == editor + 0xa0, "play receiver"); return playing ? (byte)1 : (byte)0; })));
                Hook(profile.DirtyMask, (Mask)(owner => { Require(owner == editor + 0xa0, "dirty mask receiver"); return 0x1f; }));
                Marshal.WriteIntPtr(apiTable, 0x70, Root((Dirty)((owner, mask) => { Require(owner == editor + 0xa0 && mask == 0x1f, "dirty check ABI"); return dirty ? (byte)1 : (byte)0; })));
                Hook(profile.ResourceNameConstructor, (Construct)((destination, source) =>
                {
                    Require(Marshal.PtrToStringUTF8(source) == "Fixture.ent", "resource constructor input");
                    var resource = Alloc(24); Marshal.WriteInt32(resource, 1); Marshal.WriteIntPtr(resource, 8, Text(resourcePath)); resources.Add(resource); Marshal.WriteIntPtr(destination, resource);
                }));
                Hook(profile.ResourceNameDestroy, (Destroy)(resource => { Require(resources.Contains(resource) && Marshal.ReadInt32(resource) == 0, "resource destructor lifetime"); destroys++; }));
                Marshal.WriteIntPtr(poolTable, 0x10, Root((PoolFree)((owner, resource, zero) => { Require(owner == pool && resources.Contains(resource) && zero == 0, "pool release ABI"); poolFrees++; })));
                Hook(profile.FileSystemOwner, (Owner)(() => fs));
                Marshal.WriteIntPtr(fsTable, 0x148, Root((Resolve)((owner, source, destination, index) =>
                {
                    Require(owner == fs && index == -1 && Marshal.PtrToStringUTF8(source) == resourcePath, "filesystem resolution ABI");
                    if (profile.Limited) { var text = Text(resolvedPath, refs); strings.Add(text); Marshal.WriteIntPtr(destination, text); }
                    else { var bytes = Encoding.UTF8.GetBytes(resolvedPath + "\0"); Marshal.Copy(bytes, 0, destination, bytes.Length); }
                    return resolved ? (byte)1 : (byte)0;
                })));
                if (profile.Limited) Hook(profile.ReleaseString, (Free)((allocation, zero) => { Require(zero == 0 && strings.Contains(allocation + 12) && Marshal.ReadInt32(allocation, 8) == 0, "resolved string release ABI"); stringFrees++; }));
                Marshal.WriteIntPtr(fsTable, 0x150, Root((Exists)((owner, path, index) => { Require(owner == fs && index == -1 && Marshal.PtrToStringUTF8(path) == resolvedPath, "file exists ABI"); return exists ? (byte)1 : (byte)0; })));
                Hook(profile.OpenResource, (Open)((owner, resource) => { Require(owner == editor && resources.Contains(Marshal.ReadIntPtr(resource)), "OpenResource ABI"); opens++; Marshal.WriteIntPtr(document, 0x50, Text(resourcePath)); return 1; }));
                void Run()
                {
                    Marshal.Copy(new byte[4096], 0, output, 4096); Marshal.WriteIntPtr(output, 16, app);
                    var bytes = Encoding.UTF8.GetBytes("Fixture.ent\0"); Marshal.Copy(bytes, 0, output + 512, bytes.Length);
                    var code = NativeWorkbench.OpenWorldProbe(image, profile); Marshal.Copy(code, 0, image, code.Length);
                    Require(Win.FlushInstructionCache(Win.GetCurrentProcess(), image, 0x4000000), "flush"); Marshal.GetDelegateForFunctionPointer<Probe>(image)(output);
                }
                Run(); Require(opens == 1 && destroys == 1 && poolFrees == 1 && Marshal.ReadInt32(output, 68) == 1 && Marshal.PtrToStringUTF8(output + 2048) == "Fixture.ent", "valid open and cleanup/readback");
                exists = false; Run(); Require(opens == 1 && destroys == 2 && poolFrees == 2, "missing file cannot unload"); exists = true;
                resolved = false; Run(); Require(opens == 1 && destroys == 3 && poolFrees == 3, "resolution failure cleanup"); resolved = true;
                resolvedPath = new string('a', 241); Run(); Require(opens == 1 && destroys == 4 && poolFrees == 4 && Marshal.ReadInt32(output, 72) == 6, "expanded path overflow cleanup"); resolvedPath = "C:/Fixture/Fixture.ent";
                resourcePath = "Fixture.bad"; Run(); Require(opens == 1 && destroys == 5 && poolFrees == 5, "wrong extension rejected"); resourcePath = "Fixture.ent";
                playing = true; Run(); Require(opens == 1 && destroys == 5 && Marshal.ReadInt32(output, 72) == 5, "play precondition"); playing = false;
                dirty = true; Run(); Require(opens == 1 && destroys == 5 && Marshal.ReadInt32(output, 72) == 5, "dirty precondition"); dirty = false;
                Marshal.WriteIntPtr(editor, 0); Run(); Require(opens == 1 && Marshal.ReadInt32(output, 72) == 4, "wrong vtable rejected"); Marshal.WriteIntPtr(editor, image + profile.EditorVtable);
                missing = true; Run(); Require(opens == 1 && creates == 0, "missing descriptor"); missing = false;
                Marshal.WriteInt32(descriptor, 0x24, 0); Run(); Require(opens == 2 && creates == 1 && destroys == 6 && poolFrees == 6, "creates missing editor once");
                if (profile.Limited)
                {
                    Require(stringFrees == 5, "owned path freed for success/failure/overflow");
                    refs = 2; Run(); Require(stringFrees == 5 && Marshal.ReadInt32(strings[^1] - 4) == 1 && opens == 3, "shared resolved path reference retained");
                }
                Console.WriteLine("PASS " + (profile.Limited ? "1.9" : "legacy") + " world open ABI, path guards, preconditions, resource and string ownership");
            }
            finally { GC.KeepAlive(roots); foreach (var p in allocations) Marshal.FreeHGlobal(p); Win.VirtualFree(image, 0, 0x8000); }
        }
    }
}
