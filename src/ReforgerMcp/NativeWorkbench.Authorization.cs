namespace ReforgerMcp;

internal static partial class NativeWorkbench
{
    // WorkbenchVMErrorHandler: matching object layout in the two exact profiles. The startup parameter
    // -scriptAuthorizeAll calls this same owner; no dialog text or code patch.
    internal static bool ValidScriptAuthorizationDecision(int decision) =>
        decision is 0 or 0x4000 or 0x8000 or 0x10000 or 0x20000;

    private static void EmitAllowScriptAuthorization(E e, nint image, int setter)
    {
        e.B(0x48, 0x8b, 0x4b, 16, 0x48, 0x81, 0xc1, 0xb0, 0x12, 0, 0);
        e.B(0x48, 0xb8); e.Q(image + setter); e.B(0xff, 0xd0);
    }

    private static void EmitReadScriptAuthorization(E e, byte outputOffset)
    {
        e.B(0x48, 0x8b, 0x43, 16, 0x8b, 0x80, 0xcc, 0x12, 0, 0, 0x89, 0x43, outputOffset);
    }
}


