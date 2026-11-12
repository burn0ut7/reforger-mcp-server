using System.Runtime.InteropServices;
using ReforgerMcp;

internal static class ArrayAddCallbackChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Probe(nint output);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Cast(nint item, int delta, nint source, nint target, int reference);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Item(nint view, nint index);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate byte Enabled(nint widget, nint parent);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Add(nint property);

    internal static void Run()
    {
        foreach (var profile in new[] { NativeWorkbench.ArrayAddProfile.Legacy, NativeWorkbench.ArrayAddProfile.Current })
        {
            var image = Win.VirtualAlloc(0, 0x4000000, 0x3000, 0x40);
            if (image == 0) throw new Exception("Cannot allocate array fixture");
            var allocations = new List<nint>();
            var roots = new List<Delegate>();
            nint Alloc(int size) { var p = Marshal.AllocHGlobal(size); Marshal.Copy(new byte[size], 0, p, size); allocations.Add(p); return p; }
            nint Root(Delegate callback) { roots.Add(callback); return Marshal.GetFunctionPointerForDelegate(callback); }
            void Hook(int rva, Delegate callback)
            {
                var bytes = new List<byte> { 0x48, 0xb8 };
                bytes.AddRange(BitConverter.GetBytes((long)Root(callback))); bytes.AddRange([0xff, 0xe0]);
                Marshal.Copy(bytes.ToArray(), 0, image + rva, bytes.Count);
            }
            void Require(bool condition, string name) { if (!condition) throw new Exception(name); }
            var output = Alloc(512); var view = Alloc(8); var item = Alloc(8);
            var property = Alloc(256); var table = Alloc(0x2a0); var editor = Alloc(64); var button = Alloc(8);
            int calls = 0;
            bool treeValid = true, itemValid = true, enabled = true, itemExists = true;
            try
            {
                Marshal.WriteIntPtr(output, 96, view); Marshal.WriteInt32(output, 288, 17);
                Marshal.WriteIntPtr(property, table); Marshal.WriteIntPtr(property, 0xa8, editor);
                Marshal.WriteIntPtr(editor, image + profile.EditorVtable); Marshal.WriteIntPtr(editor, 0x30, button);
                Hook(profile.DynamicCast, (Cast)((receiver, delta, source, target, reference) =>
                {
                    Require(delta == 0 && reference == 0, "Pointer cast ABI");
                    if (receiver == view)
                    {
                        Require(source == image + profile.WidgetType && target == image + profile.TreeType, "View RTTI identity");
                        return treeValid ? view : 0;
                    }
                    Require(receiver == item && source == image + profile.ItemType && target == image + profile.ArrayType, "Array RTTI identity");
                    return itemValid ? property : 0;
                }));
                Hook(0x34c240, (Item)((receiver, index) =>
                {
                    Require(receiver == view && index == output + 288 && Marshal.ReadInt32(index) == 17, "Copied row index ABI");
                    return itemExists ? item : 0;
                }));
                Hook(0x5c820, (Enabled)((receiver, parent) =>
                {
                    Require(receiver == button && parent == 0, "Native plus-button eligibility ABI");
                    return enabled ? (byte)1 : (byte)0;
                }));
                Marshal.WriteIntPtr(table, 0x298, Root((Add)(receiver => { Require(receiver == property, "Adjusted array owner receiver"); calls++; })));
                var code = NativeWorkbench.ArrayAddProbe(image, image, profile);
                Marshal.Copy(code, 0, image, code.Length);
                Require(Win.FlushInstructionCache(Win.GetCurrentProcess(), image, 0x4000000), "Flush array fixture");
                var run = Marshal.GetDelegateForFunctionPointer<Probe>(image);
                void Check(bool dispatch, string name)
                {
                    calls = 0; Marshal.WriteInt32(output, 68, 0); run(output);
                    Require(calls == (dispatch ? 1 : 0) && Marshal.ReadInt32(output, 68) == (dispatch ? 1 : 0), name);
                }
                Check(true, "Enabled array invokes owning virtual handler once");
                enabled = false; Check(false, "Disabled plus refuses dispatch"); enabled = true;
                treeValid = false; Check(false, "Non-tree view refuses dispatch"); treeValid = true;
                itemExists = false; Check(false, "Missing row refuses dispatch"); itemExists = true;
                itemValid = false; Check(false, "Non-array row refuses dispatch"); itemValid = true;
                Marshal.WriteIntPtr(property, 0xa8, 0); Check(false, "Missing editor refuses dispatch"); Marshal.WriteIntPtr(property, 0xa8, editor);
                Marshal.WriteIntPtr(editor, image); Check(false, "Unexpected editor identity refuses dispatch"); Marshal.WriteIntPtr(editor, image + profile.EditorVtable);
                Marshal.WriteIntPtr(editor, 0x30, 0); Check(false, "Missing plus button refuses dispatch");
                Console.WriteLine("PASS array callback ownership and refusal paths for " + profile.DynamicCast.ToString("x"));
            }
            finally
            {
                GC.KeepAlive(roots);
                foreach (var allocation in allocations) Marshal.FreeHGlobal(allocation);
                Win.VirtualFree(image, 0, 0x8000);
            }
        }
    }
}
