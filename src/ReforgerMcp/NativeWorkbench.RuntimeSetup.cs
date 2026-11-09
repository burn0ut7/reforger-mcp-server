namespace ReforgerMcp;

internal sealed record NativeRuntimeSetup(string ProfileDirectory, bool Enabled, int Port);

internal static partial class NativeWorkbench
{
    internal static byte[] RuntimeSetupProbe(nint image, bool enable, int? port, MaintenanceProfile profile)
    {
        var e = new E();
        e.B(0x53, 0x48, 0x83, 0xec, 0x40, 0x48, 0x89, 0xcb);
        EmitRuntimeSetup(e, image, enable, port, profile);
        e.Label("return"); e.B(0x48, 0x83, 0xc4, 0x40, 0x5b, 0xc3);
        return e.Done();
    }

    private static NativeRuntimeSetup ReadRuntimeSetup(byte[] header, byte[] path)
    {
        var end = Array.IndexOf(path, (byte)0);
        if (BitConverter.ToInt32(header, 72) != 2 || end <= 0)
            throw new InvalidOperationException("Workbench runtime settings/profile unavailable; no successful setup claimed.");
        var profile = PakArchive.Utf8.GetString(path, 0, end);
        var port = BitConverter.ToInt32(header, 108);
        if (!Path.IsPathFullyQualified(profile) || port is < 1 or > 65535)
            throw new InvalidOperationException("Invalid native profile or NET API port.");
        return new(Path.TrimEndingDirectorySeparator(Path.GetFullPath(profile)), header[104] != 0, port);
    }

    // Settings access follows the versioned startup override path;
    // server creation and selected-setting persistence call the owning routines.
    // No options dialog, registry patch, arbitrary address input, or VM bridge needed.
    private static void EmitRuntimeSetup(E e, nint image, bool enable, int? port, MaintenanceProfile profile)
    {
        void I(int n) => e.B(BitConverter.GetBytes(n));
        void Call(int rva) { e.B(0x48, 0xb8); e.Q(image + rva); e.B(0xff, 0xd0); }
        void Settings() { e.B(0x48, 0x8b, 0x4b, 16, 0x48, 0x8b, 0x89); I(profile.Limited ? 0x1608 : 0x1600); }
        // Resolve the actual running profile, including custom -profile launches.
        if (!profile.Limited)
        {
            Call(0x1e90630);
            e.B(0x48, 0x89, 0xc1, 0x48, 0x8b, 0x00, 0x48, 0x8d, 0x93); I(512);
            e.B(0x4c, 0x8d, 0x83); I(1024);
            e.B(0x45, 0x31, 0xc9, 0xff, 0x90); I(0x140);
            e.B(0x84, 0xc0); e.J(0x84, "setupInvalid");
        }
        else
        {
            // 1.9 returns an owned string. Mirror the project resolver's ABI and
            // release it even on failure/overflow before rejecting the operation.
            e.B(0x48, 0x8d, 0x8b); I(512);
            e.B(0x48, 0x8d, 0x93); I(640);
            e.B(0x45, 0x31, 0xc0, 0x45, 0x31, 0xc9, 0xc7, 0x44, 0x24, 0x20); I(-1);
            Call(profile.ResolvePath);
            e.B(0x88, 0x83); I(648);
            e.B(0x48, 0x8b, 0x93); I(640);
            e.B(0x48, 0x85, 0xd2); e.J(0x84, "setupInvalid");
            e.B(0x8b, 0x4a, 0xf8, 0x81, 0xf9); I(1024);
            e.J(0x82, "setupPathCopyStart");
            e.B(0xc6, 0x83); I(648); e.B(0);
            e.J(0x84, "setupPathRelease", true);
            e.Label("setupPathCopyStart");
            e.B(0x45, 0x31, 0xc9);
            e.Label("setupPathCopy");
            e.B(0x44, 0x39, 0xc9); e.J(0x84, "setupPathRelease");
            e.B(0x42, 0x8a, 0x04, 0x0a, 0x42, 0x88, 0x84, 0x0b); I(1024);
            e.B(0x41, 0xff, 0xc1); e.J(0x84, "setupPathCopy", true);
            e.Label("setupPathRelease");
            e.B(0x48, 0x89, 0xd1, 0xf0, 0xff, 0x49, 0xfc);
            e.J(0x85, "setupPathReleased");
            e.B(0x48, 0x83, 0xe9, 12, 0x31, 0xd2); Call(profile.ReleaseString);
            e.Label("setupPathReleased");
            e.B(0x48, 0xc7, 0x83); I(640); I(0);
            e.B(0x80, 0xbb); I(648); e.B(0); e.J(0x84, "setupInvalid");
        }
        if (enable)
        {
            if (port is not null)
            {
                // Same mutable setting accessor as Enable NET API. Stop only NetApi,
                // through its owner, before starting it with the requested setting.
                Settings(); e.B(0xba); I(9); e.B(0x48, 0x8b, 0x01, 0xff, 0x90); I(0x120);
                e.B(0x48, 0x85, 0xc0); e.J(0x84, "setupInvalid");
                e.B(0xc7, 0x00); I(port.Value);
                e.B(0x48, 0x8b, 0x4b, 16, 0x48, 0x8d, 0x93); I(544); Call(profile.NetStop);
            }
            Settings(); e.B(0xba); I(8); e.B(0x48, 0x8b, 0x01, 0xff, 0x90); I(0x120);
            e.B(0x48, 0x85, 0xc0); e.J(0x84, "setupInvalid");
            e.B(0xc6, 0x00, 1);
            e.B(0x48, 0x8b, 0x4b, 16, 0x48, 0x8d, 0x93); I(544); Call(profile.NetStart);
            // Engine-owned array<int> containing only the Enable NET API field.
            // Save routine consumes it and writes through Workbench's QSettings.
            e.B(0xb9); I(port is null ? 4 : 8); e.B(0x31, 0xd2); Call(profile.Allocate);
            e.B(0x48, 0x85, 0xc0); e.J(0x84, "setupInvalid");
            e.B(0xc7, 0x00); I(8); e.B(0x48, 0x89, 0x83); I(256);
            if (port is not null) { e.B(0xc7, 0x40, 4); I(9); }
            e.B(0xc7, 0x83); I(264); I(port is null ? 1 : 2); e.B(0xc7, 0x83); I(268); I(port is null ? 1 : 2);
            e.B(0x48, 0x8b, 0x4b, 16, 0x48, 0x8d, 0x93); I(256); Call(profile.SaveSettings);
        }
        foreach (var (id, offset) in new[] { (8, 104), (9, 108) })
        {
            Settings(); e.B(0xba); I(id); e.B(0x48, 0x8b, 0x01, 0xff, 0x90); I(0x128);
            e.B(0x48, 0x85, 0xc0); e.J(0x85, "setting" + id);
            Settings(); e.B(0x48, 0x8b, 0x41, 24, 0x48, 0x8b, 0x40, 40, 0x48, 0x8b, 0x80); I(id * 0x58 + 16);
            e.Label("setting" + id);
            e.B(0x48, 0x85, 0xc0); e.J(0x84, "setupInvalid");
            if (id == 8) e.B(0x0f, 0xb6, 0x00); else e.B(0x8b, 0x00);
            e.B(0x89, 0x43, (byte)offset);
        }
        e.B(0xc7, 0x43, 72, 2, 0, 0, 0); e.J(0x84, "return", true);
        e.Label("setupInvalid"); e.B(0xc7, 0x43, 72, 29, 0, 0, 0); e.J(0x84, "return", true);
    }
}
