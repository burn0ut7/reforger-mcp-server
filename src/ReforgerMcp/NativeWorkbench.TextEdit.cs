namespace ReforgerMcp;

internal static partial class NativeWorkbench
{
    // Qt 6.8.3 exports, under the existing exact module hash checks. The caller
    // resolves and validates the accessible object's lifetime on the UI thread.
    private static void EmitFinishEdit(E e, nint widgets, nint core)
    {
        void Call(nint address) { e.B(0x48, 0xb8); e.Q(address); e.B(0xff, 0xd0); }
        e.B(0x48, 0x8b, 0x4b, 96, 0x48, 0x8b, 0x01, 0xff, 0x50, 0x10); // QAccessibleInterface::object
        e.B(0x48, 0x85, 0xc0); e.J(0x84, "uiDone");
        e.B(0x48, 0x89, 0xc2, 0x48, 0xb9); e.Q(widgets + 0x48fd40);
        Call(core + 0x38f0); // QLineEdit::staticMetaObject.cast(object)
        e.B(0x48, 0x85, 0xc0); e.J(0x84, "uiDone");
        e.B(0x48, 0x89, 0x43, 112, 0x48, 0x89, 0xc1);
        Call(widgets + 0x185c40); // isReadOnly
        e.B(0x84, 0xc0); e.J(0x85, "uiDone");
        e.B(0x48, 0x8b, 0x4b, 112, 0x31, 0xd2); Call(widgets + 0x5c820); // isEnabledTo(nullptr)
        e.B(0x84, 0xc0); e.J(0x84, "uiDone");
        e.B(0x48, 0x8b, 0x4b, 112); Call(widgets + 0x185490); // hasAcceptableInput
        e.B(0x84, 0xc0); e.J(0x84, "uiDone");
        e.B(0x48, 0x8b, 0x4b, 112); Call(widgets + 0x184d90); // editingFinished
        // Do not access the widget after signals: its owner may destroy it.
    }

    internal static byte[] FinishEditProbe(nint widgets, nint core)
    {
        var e = new E();
        e.B(0x53, 0x48, 0x83, 0xec, 0x40, 0x48, 0x89, 0xcb);
        EmitFinishEdit(e, widgets, core);
        e.B(0xc7, 0x43, 68, 1, 0, 0, 0);
        e.Label("uiDone");
        e.B(0x48, 0x83, 0xc4, 0x40, 0x5b, 0xc3);
        return e.Done();
    }
}
