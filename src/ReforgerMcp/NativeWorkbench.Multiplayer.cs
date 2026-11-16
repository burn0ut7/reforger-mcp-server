namespace ReforgerMcp;

internal static partial class NativeWorkbench
{
    // Exact executable profiles; layouts and owning functions are documented in
    // workbench-multiplayer.md. Never apply legacy addresses to a newer binary.
    internal sealed record MultiplayerProfile(int MenuVtable, int Run, int Configure, int Stop, int Start,
        int Field, int Object, int IsString, int AssignString, int ProcessType)
    {
        internal int[] Code => [Run, Configure, Stop, Start, Field, Object, IsString, AssignString];
        internal static readonly MultiplayerProfile Legacy = new(0x2bab900,
            0x2eae90, 0x2e9060, 0x2e9510, 0x2e9770, 0x1e9d100, 0x1f6f0b0, 0x1f6e490, 0x1f6ea40, 0x200001ad);
        internal static readonly MultiplayerProfile Current = new(0x2c3a340,
            0x322970, 0x320af0, 0x320fa0, 0x321200, 0x1ef4820, 0x1fe9f40, 0x1fe92c0, 0x1fe98d0, 0x200001a6);
    }

    internal static byte[] MultiplayerProbe(nint image, PluginCommand command, MaintenanceProfile profile)
    {
        var e = new E();
        e.B(0x53, 0x48, 0x83, 0xec, 0x40, 0x48, 0x89, 0xcb);
        EmitPlugin(e, image, command, profile);
        e.B(0x48, 0x83, 0xc4, 0x40, 0x5b, 0xc3);
        return e.Done();
    }
}
