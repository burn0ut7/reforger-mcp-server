namespace ReforgerMcp;

internal static partial class NativeWorkbench
{
    // Workbench 1.9.0.37 only. Invoke SearchListWidget's item-pressed owner,
    // including its virtual acceptance check; never write its selected-item field.
    private static void EmitPickerActivate(E e, nint image, nint widgets, long window)
    {
        void Call(nint address) { e.B(0x48, 0xb8); e.Q(address); e.B(0xff, 0xd0); }
        e.B(0x48, 0x89, 0x4b, 112); // live view, validated accessible-cell lifetime
        e.B(0x48, 0xb9); e.Q(window); Call(widgets + 0x577a0); // QWidget::find
        e.B(0x48, 0x85, 0xc0); e.J(0x84, "uiDone");
        e.B(0x48, 0xba); e.Q(image + 0x2e2cf68);
        e.B(0x48, 0x39, 0x10); e.J(0x85, "uiDone"); // exact SearchListWidget
        e.B(0x48, 0x8b, 0x4b, 112, 0x48, 0x39, 0x48, 0x30); e.J(0x85, "uiDone");
        e.B(0x48, 0x89, 0x43, 120, 0x48, 0x8d, 0x93, 0x20, 1, 0, 0);
        Call(widgets + 0x34c240); // the owned QTreeWidget's itemFromIndex
        e.B(0x48, 0x85, 0xc0); e.J(0x84, "uiDone");
        e.B(0x48, 0x89, 0xc2, 0x48, 0x8b, 0x4b, 120);
        Call(image + 0x51e5e0); // validates item, then closes picker if accepted
    }

    internal static byte[] PickerProbe(nint image, nint widgets, long window)
    {
        var e = new E();
        e.B(0x53, 0x48, 0x83, 0xec, 0x40, 0x48, 0x89, 0xcb, 0x48, 0x8b, 0x4b, 96);
        EmitPickerActivate(e, image, widgets, window);
        e.B(0xc7, 0x43, 68, 1, 0, 0, 0);
        e.Label("uiDone");
        e.B(0x48, 0x83, 0xc4, 0x40, 0x5b, 0xc3);
        return e.Done();
    }
}
