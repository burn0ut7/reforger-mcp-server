using System.Security.Cryptography;

namespace ReforgerMcp;

internal static partial class NativeWorkbench
{
    // Exact binary profiles. Limited marks the staged 1.9 port; its unported
    // capabilities reject dispatch instead of using the complete legacy profile.
    internal sealed record MaintenanceProfile(bool Limited, string Hash, int Application, int ApplicationVtable,
        int Busy, int AuthorizationSetter, int AuthorizationHandler, int AuthorizationVtable,
        int LeakLatch, int[] LeakCode, int ProjectOwner, int[] ProjectCode, int ResolvePath, int ReleaseString,
        int Reload, int EditorGetter, int EditorName, int EditorVtable, int PlayAction, int PlayCore,
        int SettingsVtable, int EnabledField, int PortField, int Allocate, int NetStart, int NetStop, int SaveSettings, int SerializeSettings,
        int[] SettingsAccessors, int OpenEditor, int OpenResource, int DirtyMask, int ResourceNameConstructor, int ResourceNameDestroy, int ResourceNamePool, int FileSystemOwner)
    {
        internal int ReloadBlockedOffset => Limited ? 0x1656 : 0x164e;
        internal ArrayAddProfile Arrays => Limited ? ArrayAddProfile.Current : ArrayAddProfile.Legacy;
        // Array add additionally uses this exact binary's validated RTTI/owner profile.
        internal bool SupportsUi(string action) => !Limited || action is
            "close" or "invoke" or "menu" or "toggle" or "set_text" or "finish_edit" or
            "select" or "activate" or "expand" or "collapse" or "open_choices" or "choose" or "add_item";
        internal static readonly MaintenanceProfile Legacy = new(false,
            "7dd36ca1f662b0c13669a4f2f7f10e3c461c2906b021e742272d64b3ab7e9798",
            0x3db6598, 0x2b922c8, 0x3db65e5, 0x298d00, 0x2980f0, 0x2bac1c8,
            0x3edf9f0, [0x1a6a881, 0x1a6a8a1, 0x1a6a8bc], 0x1ea3f00,
            [0x1ea3f00, 0x1ea3e70, 0x2061610, 0x2060e60, 0x2060c80, 0x2060d70, 0x1e90630, 0x3a9a10], 0, 0,
            0x222c70, 0x20ea70, 0x2ba4318, 0x2ba8d78, 0x27f050, 0x27f070,
            0x3221a60, 0x3b0dbb0, 0x3b0dbb4, 0x1e32460, 0x22a320, 0x22a710, 0x22c230, 0x22c310,
            [0x57d160, 0x57d1a0], 0x20fec0, 0x2832b0, 0x1a7a900, 0x1e35d50, 0x269dc0, 0x3c7c260, 0x1e90630);
        internal static readonly MaintenanceProfile Current = new(true,
            "456a3e4e3b55f47d89c09e4364df78d9a0b1f7348bdc2354cce3b34e1db70b28",
            0x3e881e8, 0x2c1c2f8, 0x3e88235, 0x2d0420, 0x2cf810, 0x2c3ac08,
            0x3fb2360, [0x1aaa691, 0x1aaa6a7, 0x1aaa6cc], 0x1f07120,
            [0x1f07120, 0x1f07090, 0x20d19c0, 0x20d11e0, 0x20d1060, 0x20d1120,
             0x2019c0, 0x201910, 0x1efd690, 0x1e95e70], 0x2019c0, 0x1e95e70,
            0x22ca80, 0x219cf0, 0x2c2f5c0, 0x2c34418, 0x28a2d0, 0x28a3b0,
            0x32b7828, 0x3bd65c0, 0x3bd65c4, 0x1e953c0, 0x2343e0, 0x234920, 0x236440, 0x236520,
            [0x5956b0, 0x5956f0], 0x21af40, 0x28e660, 0x1aba050, 0x1e98cb0, 0x273f70, 0x3d45a90, 0x1efd690);
        internal static MaintenanceProfile FromHash(string hash) =>
            hash.Equals(Legacy.Hash, StringComparison.OrdinalIgnoreCase) ? Legacy :
            hash.Equals(Current.Hash, StringComparison.OrdinalIgnoreCase) ? Current :
            throw new InvalidOperationException("Unsupported native Workbench binary; no validated maintenance profile for SHA256 " + hash);
    }

    private static MaintenanceProfile Profile(string path, VerificationScope verification)
    {
        // Hold the same no-write/no-delete lease throughout selection and dispatch.
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var profile = MaintenanceProfile.FromHash(Convert.ToHexString(SHA256.HashData(file)));
        verification.Verify(path, profile.Hash);
        return profile;
    }
}


