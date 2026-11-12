namespace ReforgerMcp;

internal static partial class NativeWorkbench
{
    internal sealed record ArrayAddProfile(int DynamicCast, int WidgetType, int TreeType,
        int ItemType, int ArrayType, int EditorVtable, int[] Code)
    {
        internal static readonly ArrayAddProfile Legacy = new(0x2a68079, 0x3d15228, 0x3d16380,
            0x3d155f0, 0x3ccb080, 0x2d89d98, [0x2a68079, 0x48e210, 0x479130, 0x4e46c0]);
        internal static readonly ArrayAddProfile Current = new(0x2aee1e9, 0x3de1378, 0x3de28b0,
            0x3de1740, 0x3d950a0, 0x2e191b8, [0x2aee1e9, 0x4ad0c0, 0x4a0840, 0x4ba9e0, 0x4a8620, 0x4c5df0]);
    }

    // Exact Workbench profiles / Qt 6.8.3. Resolve the observed row through Qt,
    // then invoke ArrayProperty's owning add handler (including subtype overrides).
    // No array memory writes, simulated button click, or plugin-specific behavior.
    private static void EmitArrayAdd(E e, nint image, nint widgets, ArrayAddProfile profile)
    {
        void Call(nint address) { e.B(0x48, 0xb8); e.Q(address); e.B(0xff, 0xd0); }
        void Cast(int source, int target)
        {
            e.B(0x31, 0xd2, 0x49, 0xb8); e.Q(image + source);
            e.B(0x49, 0xb9); e.Q(image + target);
            e.B(0xc7, 0x44, 0x24, 0x20, 0, 0, 0, 0); // pointer cast: return null on mismatch
            Call(image + profile.DynamicCast); // __RTDynamicCast
            e.B(0x48, 0x85, 0xc0); e.J(0x84, "uiDone");
            e.B(0x48, 0x89, 0xc1);
        }

        // RCX is the accessible cell's live view. The copied QModelIndex is +288.
        Cast(profile.WidgetType, profile.TreeType); // QWidget -> QTreeWidget
        e.B(0x48, 0x8d, 0x93, 0x20, 1, 0, 0);
        Call(widgets + 0x34c240); // QTreeWidget::itemFromIndex
        e.B(0x48, 0x85, 0xc0); e.J(0x84, "uiDone");
        e.B(0x48, 0x89, 0xc1);
        Cast(profile.ItemType, profile.ArrayType); // QTreeWidgetItem -> ArrayProperty; adjusts multiple inheritance
        e.B(0x48, 0x89, 0x43, 112);

        // Respect the native '+' enabled state, including parent restrictions.
        e.B(0x48, 0x8b, 0x88, 0xa8, 0, 0, 0, 0x48, 0x85, 0xc9); e.J(0x84, "uiDone");
        e.B(0x48, 0xba); e.Q(image + profile.EditorVtable); // ArrayEditWidget
        e.B(0x48, 0x39, 0x11); e.J(0x85, "uiDone");
        e.B(0x48, 0x8b, 0x49, 0x30, 0x48, 0x85, 0xc9); e.J(0x84, "uiDone");
        e.B(0x31, 0xd2); Call(widgets + 0x5c820); // QWidget::isEnabledTo(nullptr)
        e.B(0x84, 0xc0); e.J(0x84, "uiDone");

        e.B(0x48, 0x8b, 0x4b, 112, 0x48, 0x8b, 0x01, 0xff, 0x90, 0x98, 2, 0, 0);
        // The normal handler can open a type picker or cancel creation. EmitUi
        // reports dispatch only; callers must inspect the resulting list.
    }

    internal static byte[] ArrayAddProbe(nint image, nint widgets, ArrayAddProfile profile)
    {
        var e = new E();
        e.B(0x53, 0x48, 0x83, 0xec, 0x40, 0x48, 0x89, 0xcb);
        e.B(0x48, 0x8b, 0x4b, 96);
        EmitArrayAdd(e, image, widgets, profile);
        e.B(0xc7, 0x43, 68, 1, 0, 0, 0);
        e.Label("uiDone");
        e.B(0x48, 0x83, 0xc4, 0x40, 0x5b, 0xc3);
        return e.Done();
    }
}
