using System.Runtime.InteropServices;
using System.Text;
using ReforgerMcp;

internal static class RuntimeSetupChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(nint output);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Owner();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte RawResolve(nint fs, nint path, nint result, int zero);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Resolve(nint path, nint result, int absolute, int normalize, int index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Free(nint allocation, int zero);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Field(nint settings, int id);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Server(nint app, nint name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Allocate(int bytes, int zero);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Save(nint app, nint fields);

    internal static void Run()
    {
        foreach (var profile in new[] { NativeWorkbench.MaintenanceProfile.Legacy, NativeWorkbench.MaintenanceProfile.Current })
        {
            var image = Win.VirtualAlloc(0, 0x3300000, 0x3000, 0x40);
            if (image == 0) throw new Exception("Cannot allocate runtime setup fixture");
            var allocations = new List<nint>(); var roots = new List<Delegate>();
            nint Alloc(int bytes) { var p = Marshal.AllocHGlobal(bytes); Marshal.Copy(new byte[bytes], 0, p, bytes); allocations.Add(p); return p; }
            nint Root(Delegate value) { roots.Add(value); return Marshal.GetFunctionPointerForDelegate(value); }
            void Hook(int rva, Delegate callback)
            {
                var code = new List<byte> { 0x48, 0xb8 }; code.AddRange(BitConverter.GetBytes((long)Root(callback))); code.AddRange([0xff, 0xe0]);
                Marshal.Copy(code.ToArray(), 0, image + rva, code.Count);
            }
            void Require(bool value, string message) { if (!value) throw new Exception(message); }
            var output = Alloc(4096); var app = Alloc(0x1700); var settings = Alloc(64); var table = Alloc(0x150);
            var enabled = Alloc(4); var port = Alloc(4); var descriptor = Alloc(64); var defaults = Alloc(10 * 0x58);
            var fs = Alloc(8); var fsTable = Alloc(0x150);
            int starts = 0, stops = 0, saves = 0, frees = 0, references = 1;
            bool useDefaults = false, resolveSuccess = true;
            string path = "C:/FixtureProfile/";
            var returned = new List<nint>();
            try
            {
                Marshal.WriteIntPtr(app, profile.Limited ? 0x1608 : 0x1600, settings);
                Marshal.WriteIntPtr(settings, table); Marshal.WriteIntPtr(settings, 24, descriptor);
                Marshal.WriteIntPtr(descriptor, 40, defaults);
                Marshal.WriteIntPtr(defaults, 8 * 0x58 + 16, enabled); Marshal.WriteIntPtr(defaults, 9 * 0x58 + 16, port);
                Marshal.WriteInt32(port, 5775);
                nint Access(nint owner, int id) { Require(owner == settings && id is 8 or 9, "setting access ABI"); return id == 8 ? enabled : port; }
                Marshal.WriteIntPtr(table, 0x120, Root((Field)Access));
                Marshal.WriteIntPtr(table, 0x128, Root((Field)((owner, id) => useDefaults ? 0 : Access(owner, id))));
                if (profile.Limited)
                {
                    Hook(profile.ResolvePath, (Resolve)((source, destination, absolute, normalize, index) =>
                    {
                        Require(Marshal.PtrToStringUTF8(source) == "$profile:" && absolute == 0 && normalize == 0 && index == -1, "owned path ABI");
                        var bytes = Encoding.UTF8.GetBytes(path); var p = Alloc(bytes.Length + 13);
                        Marshal.WriteInt32(p, 4, bytes.Length); Marshal.WriteInt32(p, 8, references); Marshal.Copy(bytes, 0, p + 12, bytes.Length);
                        returned.Add(p); Marshal.WriteIntPtr(destination, p + 12); return resolveSuccess ? (byte)1 : (byte)0;
                    }));
                    Hook(profile.ReleaseString, (Free)((p, zero) => { Require(zero == 0 && returned.Contains(p) && Marshal.ReadInt32(p, 8) == 0, "owned path release ABI"); frees++; }));
                }
                else
                {
                    Marshal.WriteIntPtr(fs, fsTable);
                    Hook(0x1e90630, (Owner)(() => fs));
                    Marshal.WriteIntPtr(fsTable, 0x140, Root((RawResolve)((owner, source, destination, zero) =>
                    {
                        Require(owner == fs && zero == 0 && Marshal.PtrToStringUTF8(source) == "$profile:", "legacy path ABI");
                        var bytes = Encoding.UTF8.GetBytes(path + "\0"); Marshal.Copy(bytes, 0, destination, bytes.Length); return 1;
                    })));
                }
                void NamedServer(nint owner, nint name) { Require(owner == app && Marshal.PtrToStringUTF8(name) == "NetAPI", "server owner ABI"); }
                Hook(profile.NetStart, (Server)((owner, name) => { NamedServer(owner, name); starts++; }));
                Hook(profile.NetStop, (Server)((owner, name) => { NamedServer(owner, name); stops++; }));
                Hook(profile.Allocate, (Allocate)((size, zero) => { Require(zero == 0 && size is 4 or 8, "field array allocation ABI"); return Alloc(size); }));
                Hook(profile.SaveSettings, (Save)((owner, fields) =>
                {
                    Require(owner == app, "save owner ABI"); var count = Marshal.ReadInt32(fields, 12); var data = Marshal.ReadIntPtr(fields);
                    Require(count is 1 or 2 && Marshal.ReadInt32(fields, 8) == count && Marshal.ReadInt32(data) == 8, "save selected fields");
                    if (count == 2) Require(Marshal.ReadInt32(data, 4) == 9, "save explicit port only"); saves++;
                }));
                void Run(bool enable, int? requestedPort = null)
                {
                    Marshal.Copy(new byte[4096], 0, output, 4096); Marshal.WriteIntPtr(output, 16, app);
                    var pathBytes = Encoding.UTF8.GetBytes("$profile:\0"); Marshal.Copy(pathBytes, 0, output + 512, pathBytes.Length);
                    var name = Encoding.UTF8.GetBytes("NetAPI\0"); Marshal.Copy(name, 0, output + 544, name.Length);
                    var code = NativeWorkbench.RuntimeSetupProbe(image, enable, requestedPort, profile); Marshal.Copy(code, 0, image, code.Length);
                    Require(Win.FlushInstructionCache(Win.GetCurrentProcess(), image, 0x3300000), "flush");
                    Marshal.GetDelegateForFunctionPointer<Probe>(image)(output);
                }
                Run(false); Require(Marshal.ReadInt32(output, 72) == 2 && Marshal.ReadInt32(output, 108) == 5775 && starts == 0 && saves == 0, "read only status");
                Require(Marshal.PtrToStringUTF8(output + 1024) == path, "copied profile path");
                useDefaults = true; Run(false); Require(Marshal.ReadInt32(output, 108) == 5775, "native default setting read"); useDefaults = false;
                Run(true); Require(Marshal.ReadByte(enabled) == 1 && Marshal.ReadInt32(port) == 5775 && starts == 1 && stops == 0 && saves == 1, "enable preserves port");
                Run(true, 5820); Require(Marshal.ReadInt32(port) == 5820 && starts == 2 && stops == 1 && saves == 2, "explicit port uses lifecycle");
                if (profile.Limited)
                {
                    Require(frees == 4, "owned strings released on success");
                    path = new string('x', 1024); Run(true); Require(Marshal.ReadInt32(output, 72) == 29 && frees == 5 && starts == 2, "overflow releases before mutation");
                    path = "C:/FixtureProfile/"; resolveSuccess = false; Run(true); Require(Marshal.ReadInt32(output, 72) == 29 && frees == 6 && starts == 2, "failed resolution releases before mutation");
                    resolveSuccess = true; references = 2; Run(false); Require(frees == 6 && Marshal.ReadInt32(returned[^1], 8) == 1, "shared reference retained");
                }
                Console.WriteLine("PASS " + (profile.Limited ? "1.9" : "legacy") + " runtime setup ABI/status/defaults/enable/port and string lifetime");
            }
            finally { GC.KeepAlive(roots); foreach (var allocation in allocations) Marshal.FreeHGlobal(allocation); Win.VirtualFree(image, 0, 0x8000); }
        }
    }
}
