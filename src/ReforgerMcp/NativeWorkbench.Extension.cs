using System.Text;

namespace ReforgerMcp;

internal static partial class NativeWorkbench
{
    internal sealed record ExtensionCommand(string FilePath, string Specification);
    internal sealed record ExtensionResult(string State, int ProcessId, bool ModuleLoaded, int? ReturnCodeOrWin32Error,
        uint ExpectedThread, uint ActualThread, bool AllocationsReleased);
    private const string ExtensionEntry = "RST_WorkbenchExtensionStart_v1";

    internal static byte[] ExtensionInput(ExtensionCommand command)
    {
        var bytes = new byte[3584];
        void Put(int offset, int capacity, string value)
        {
            if (value.Contains('\0') || value.Length >= capacity / 2)
                throw new ArgumentException("Extension path/specification exceeds its UTF-16 bound or contains NUL.");
            Encoding.Unicode.GetBytes(value).CopyTo(bytes, offset);
        }
        Put(0, 1536, command.FilePath);
        Put(1536, 1536, command.Specification);
        Encoding.ASCII.GetBytes(ExtensionEntry).CopyTo(bytes, 3072);
        return bytes;
    }

    // Uses the exact absolute DLL, never a Qt key search or an ambient DLL path.
    // The module remains resident even if initialization fails; loading may have
    // side effects, and automatic unload/replay cannot undo them safely.
    private static void EmitExtension(E e, nint loadLibrary, nint getProcAddress, nint getLastError)
    {
        void Call(nint address) { e.B(0x48, 0xb8); e.Q(address); e.B(0xff, 0xd0); }
        e.B(0x48, 0x8d, 0x8b, 0, 2, 0, 0, 0x31, 0xd2); // path +512; reserved=null
        e.B(0x41, 0xb8, 0, 0x0b, 0, 0); // DLL_LOAD_DIR | APPLICATION_DIR | SYSTEM32; exclude user directories
        Call(loadLibrary);
        e.B(0x48, 0x89, 0x43, 112, 0x48, 0x85, 0xc0); e.J(0x84, "extensionLoadFailed");
        e.B(0xc7, 0x43, 104, 1, 0, 0, 0); // module loaded
        e.B(0x48, 0x89, 0xc1, 0x48, 0x8d, 0x93, 0, 0x0e, 0, 0); // entry +3584
        Call(getProcAddress);
        e.B(0x48, 0x85, 0xc0); e.J(0x84, "extensionLoadFailed");
        e.B(0xc7, 0x43, 104, 2, 0, 0, 0); // entry found
        e.B(0x48, 0x8d, 0x8b, 0, 8, 0, 0, 0xff, 0xd0); // specification +2048
        e.B(0x89, 0x43, 108, 0xc7, 0x43, 68, 1, 0, 0, 0); // returned; not proof of functional readiness
        e.J(0x84, "extensionDone", true);
        e.Label("extensionLoadFailed"); Call(getLastError); e.B(0x89, 0x43, 108);
        e.Label("extensionDone");
    }

    internal static byte[] ExtensionProbe(nint loadLibrary, nint getProcAddress, nint getLastError)
    {
        var e = new E();
        e.B(0x53, 0x48, 0x83, 0xec, 0x40, 0x48, 0x89, 0xcb);
        EmitExtension(e, loadLibrary, getProcAddress, getLastError);
        e.B(0x48, 0x83, 0xc4, 0x40, 0x5b, 0xc3);
        return e.Done();
    }
}
