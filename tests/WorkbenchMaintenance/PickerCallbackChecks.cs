using System.Runtime.InteropServices;
using ReforgerMcp;

internal static class PickerCallbackChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(nint output);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Find(long window);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Item(nint view, nint index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Activate(nint picker, nint item);

    internal static void Run()
    {
        var image = Win.VirtualAlloc(0, 0x3000000, 0x3000, 0x40);
        if (image == 0) throw new Exception("Cannot allocate picker fixture");
        var allocations = new List<nint>(); var roots = new List<Delegate>();
        nint Alloc(int size) { var p = Marshal.AllocHGlobal(size); Marshal.Copy(new byte[size], 0, p, size); allocations.Add(p); return p; }
        void Hook(int rva, Delegate callback)
        {
            roots.Add(callback);
            var bytes = new List<byte> { 0x48, 0xb8 };
            bytes.AddRange(BitConverter.GetBytes((long)Marshal.GetFunctionPointerForDelegate(callback))); bytes.AddRange([0xff, 0xe0]);
            Marshal.Copy(bytes.ToArray(), 0, image + rva, bytes.Count);
        }
        void Require(bool condition, string name) { if (!condition) throw new Exception(name); }
        var output = Alloc(512); var picker = Alloc(64); var view = Alloc(8); var item = Alloc(8);
        bool exists = true, rowExists = true; int calls = 0, resolves = 0;
        try
        {
            Marshal.WriteIntPtr(output, 96, view); Marshal.WriteInt32(output, 288, 17);
            Marshal.WriteIntPtr(picker, image + 0x2e2cf68); Marshal.WriteIntPtr(picker, 0x30, view);
            Hook(0x577a0, (Find)(window => { Require(window == 123, "Observed picker handle ABI"); return exists ? picker : 0; }));
            Hook(0x34c240, (Item)((owner, index) =>
            {
                Require(owner == view && index == output + 288 && Marshal.ReadInt32(index) == 17, "Picker copied-index ABI");
                resolves++; return rowExists ? item : 0;
            }));
            Hook(0x51e5e0, (Activate)((owner, selected) =>
            {
                Require(owner == picker && selected == item, "Picker activation owner and item ABI");
                calls++;
            }));
            var code = NativeWorkbench.PickerProbe(image, image, 123);
            Marshal.Copy(code, 0, image, code.Length);
            Require(Win.FlushInstructionCache(Win.GetCurrentProcess(), image, 0x3000000), "Flush picker fixture");
            var run = Marshal.GetDelegateForFunctionPointer<Probe>(image);
            void Check(int expectedCalls, int expectedResolves, string name)
            {
                calls = 0; resolves = 0; Marshal.WriteInt32(output, 68, 0); run(output);
                Require(calls == expectedCalls && resolves == expectedResolves && Marshal.ReadInt32(output, 68) == expectedCalls, name);
            }
            Check(1, 1, "Owned row activates exactly once");
            exists = false; Check(0, 0, "Missing window refuses activation"); exists = true;
            Marshal.WriteIntPtr(picker, image); Check(0, 0, "Wrong picker type refuses before row access"); Marshal.WriteIntPtr(picker, image + 0x2e2cf68);
            Marshal.WriteIntPtr(picker, 0x30, item); Check(0, 0, "Foreign view refuses before row access"); Marshal.WriteIntPtr(picker, 0x30, view);
            rowExists = false; Check(0, 1, "Missing row refuses activation");
            Console.WriteLine("PASS picker callback identity, row ownership and activation ABI");
        }
        finally
        {
            GC.KeepAlive(roots);
            foreach (var allocation in allocations) Marshal.FreeHGlobal(allocation);
            Win.VirtualFree(image, 0, 0x8000);
        }
    }
}
