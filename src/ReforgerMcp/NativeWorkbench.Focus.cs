using System.Diagnostics;
using System.Text;

namespace ReforgerMcp;

internal static partial class NativeWorkbench
{
    // Qt 6.8.3 Windows platform's owning Win32 imports. No system DLL is patched.
    private static readonly (string Name, int Rva)[] FocusImports =
    [
        ("SetForegroundWindow", 0x90e88), ("SetActiveWindow", 0x90e98),
        ("SetFocus", 0x90ec8), ("BringWindowToTop", 0x90ed0),
        ("SetWindowPlacement", 0x90ee0), ("FlashWindowEx", 0x90ef0),
        ("ShowWindow", 0x90f08), ("SetWindowPos", 0x91240)
    ];
    private const int FocusStride = 512, FocusAllocation = 8192;
    private static readonly byte[] FocusMagic = Encoding.ASCII.GetBytes("RSTFOC01");

    // Import slots are the discovery anchor across MCP restarts. Published code,
    // unwind tables and counters live until Workbench exits, even when disabled.
    internal static object FocusPolicy(Process process, string action)
    {
        if (action is not ("ensure" or "status" or "enable" or "disable"))
            throw new ArgumentException("action must be status, enable or disable.");
        using var mutex = new Mutex(false, $"Local\\ReforgerMcp.Focus.{process.Id}.{process.StartTime.ToUniversalTime().Ticks}");
        bool acquired;
        try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) throw new WorkbenchBusyException("Background focus policy is being updated.");
        try { return FocusPolicyCore(process, action); }
        finally { mutex.ReleaseMutex(); }
    }

    private static object FocusPolicyCore(Process process, string action)
    {
        using var verification = new VerificationScope();
        _ = Profile(process.MainModule!.FileName, verification);
        var qt = process.Modules.Cast<ProcessModule>().SingleOrDefault(m => m.ModuleName == "qwindows.dll")
            ?? throw new WorkbenchBusyException("Waiting for the Workbench Windows platform module.");
        verification.Verify(qt.FileName, "12577a7c4f2230bbea53f279573d7e3d1a08d631197855481e1e1580f61bb877");
        var hp = N.OpenProcess(0x10043a, false, process.Id); Check(hp != 0);
        try
        {
            byte[] Read(nint address, int size)
            {
                var bytes = new byte[size];
                Check(N.ReadProcessMemory(hp, address, bytes, (nuint)size, out var got) && got == (nuint)size);
                return bytes;
            }
            void Write(nint address, byte[] bytes) =>
                Check(N.WriteProcessMemory(hp, address, bytes, (nuint)bytes.Length, out var got) && got == (nuint)bytes.Length);
            nint Export(string library, string name)
            {
                var local = N.GetProcAddress(N.GetModuleHandle(library), name); Check(local != 0);
                Check(N.GetModuleHandleEx(6, local, out var owner));
                var path = new StringBuilder(1024); Check(N.GetModuleFileName(owner, path, 1024) != 0);
                var remote = process.Modules.Cast<ProcessModule>().Single(m => string.Equals(m.FileName, path.ToString(), StringComparison.OrdinalIgnoreCase));
                return remote.BaseAddress + (local - owner);
            }
            var originals = FocusImports.Select(f => Export("user32.dll", f.Name)).ToArray();
            var foreground = Export("user32.dll", "GetForegroundWindow");
            var windowPid = Export("user32.dll", "GetWindowThreadProcessId");
            var slots = FocusImports.Select(f => qt.BaseAddress + f.Rva).ToArray();
            var pointers = slots.Select(s => (nint)BitConverter.ToInt64(Read(s, 8))).ToArray();
            nint code = 0, data = 0;
            for (var i = 0; i < slots.Length; i++)
            {
                if (pointers[i] == originals[i]) continue;
                var header = Read(pointers[i], 64);
                // Windows application compatibility can wrap Qt's ShowWindow import.
                // Preserve the verified shim as the pass-through target, including on reattachment.
                var installed = header.AsSpan(8, 8).SequenceEqual(FocusMagic);
                var original = installed ? (nint)BitConverter.ToInt64(header, 16) : pointers[i];
                if (FocusImports[i].Name == "ShowWindow" && original != originals[i])
                {
                    var shim = process.Modules.Cast<ProcessModule>().SingleOrDefault(m =>
                        string.Equals(m.FileName, Path.Combine(Environment.SystemDirectory, "apphelp.dll"), StringComparison.OrdinalIgnoreCase));
                    if (shim != null && original == shim.BaseAddress + 0x2c2a0)
                    {
                        verification.Verify(shim.FileName, "b838911cb7279ba34a5092d35037223188c452cf282d03a2e70b7242c9516bb2");
                        originals[i] = original;
                        if (!installed) continue;
                    }
                }
                if (!header.AsSpan(8, 8).SequenceEqual(FocusMagic))
                    throw new InvalidOperationException("Focus import is owned by another hook or incompatible profile: " + FocusImports[i].Name);
                var candidateCode = pointers[i] - i * FocusStride;
                var candidateData = (nint)BitConverter.ToInt64(header, 24);
                if (code != 0 && (code != candidateCode || data != candidateData))
                    throw new InvalidOperationException("Inconsistent background focus hook ownership.");
                code = candidateCode; data = candidateData;
                if (!Read(pointers[i], FocusStride).SequenceEqual(FocusStub(i, originals[i], data, foreground, windowPid, process.Id)))
                    throw new InvalidOperationException("Background focus hook code changed; no update performed.");
            }
            if (code != 0 && pointers.Where((p, i) => p != code + i * FocusStride).Any())
                throw new InvalidOperationException("Background focus installation is partial; restart Workbench after preserving work. No editor operation dispatched.");
            if (code == 0 && action == "status")
                return new { state = "not_installed", processId = process.Id };
            if (code == 0)
            {
                data = N.VirtualAllocEx(hp, 0, 4096, 0x3000, 4); Check(data != 0);
                code = N.VirtualAllocEx(hp, 0, FocusAllocation, 0x3000, 4); Check(code != 0);
                // Start disabled. Publish all imports atomically one pointer at a time,
                // then enable only after every slot has been read back successfully.
                Write(data, FocusMagic);
                var bytes = new byte[FocusAllocation];
                for (var i = 0; i < slots.Length; i++)
                {
                    FocusStub(i, originals[i], data, foreground, windowPid, process.Id).CopyTo(bytes, i * FocusStride);
                    BitConverter.GetBytes(i * FocusStride + 64).CopyTo(bytes, 4096 + i * 12);
                    BitConverter.GetBytes((i + 1) * FocusStride).CopyTo(bytes, 4100 + i * 12);
                    BitConverter.GetBytes(4224).CopyTo(bytes, 4104 + i * 12);
                }
                // UNWIND_INFO: sub rsp,0xa8 (UWOP_ALLOC_LARGE, 168 / 8).
                new byte[] { 1, 7, 2, 0, 7, 1, 21, 0 }.CopyTo(bytes, 4224);
                var install = new E();
                install.B(0x48, 0x83, 0xec, 0x28);
                install.B(0x48, 0xb9); install.Q(code + 4096);
                install.B(0xba); install.B(BitConverter.GetBytes(slots.Length));
                install.B(0x49, 0xb8); install.Q(code);
                install.B(0x48, 0xb8); install.Q(Export("kernel32.dll", "RtlAddFunctionTable"));
                install.B(0xff, 0xd0, 0x84, 0xc0); install.J(0x84, "failed");
                for (var i = 0; i < slots.Length; i++)
                {
                    install.B(0x48, 0xb9); install.Q(slots[i]);
                    install.B(0x48, 0xb8); install.Q(originals[i]);
                    install.B(0x48, 0xba); install.Q(code + i * FocusStride);
                    install.B(0xf0, 0x48, 0x0f, 0xb1, 0x11); // lock cmpxchg [rcx],rdx
                    install.J(0x85, "failed");
                }
                install.B(0xb8, 1, 0, 0, 0); install.J(0x84, "done", true);
                install.Label("failed"); install.B(0x31, 0xc0);
                install.Label("done");
                install.B(0x48, 0xba); install.Q(data);
                install.B(0x88, 0x42, 9, 0x48, 0x83, 0xc4, 0x28, 0xc3);
                install.Done().CopyTo(bytes, 4352);
                Write(code, bytes);
                Check(N.VirtualProtectEx(hp, code, FocusAllocation, 0x20, out _));
                Check(N.FlushInstructionCache(hp, code, FocusAllocation));
                var protections = new List<(nint Slot, uint Protection)>();
                var safeToRestore = true;
                try
                {
                    foreach (var slot in slots)
                    {
                        Check(N.VirtualProtectEx(hp, slot, 8, 4, out var old));
                        protections.Add((slot, old));
                    }
                    var thread = N.CreateRemoteThread(hp, 0, 0, code + 4352, 0, 0, out _); Check(thread != 0);
                    try
                    {
                        safeToRestore = false;
                        if (N.WaitForSingleObject(thread, 5000) != 0)
                            throw new InvalidOperationException("Focus hook installation outcome unknown; memory retained, policy disabled. Inspect before retrying.");
                        safeToRestore = true;
                        Check(N.GetExitCodeThread(thread, out var result));
                        if (result != 1) throw new InvalidOperationException("Focus hook installation failed; memory retained, policy disabled. Restart Workbench after preserving work.");
                    }
                    finally { N.CloseHandle(thread); }
                    for (var i = 0; i < slots.Length; i++)
                        if (BitConverter.ToInt64(Read(slots[i], 8)) != code + i * FocusStride)
                            throw new InvalidOperationException("Focus hook publication could not be verified; policy remains disabled.");
                }
                finally
                {
                    // Several imports share a page: restore in reverse order.
                    if (safeToRestore)
                        foreach (var (slot, protection) in protections.AsEnumerable().Reverse())
                            Check(N.VirtualProtectEx(hp, slot, 8, protection, out _));
                }
                Write(data + 10, [1]); // All original import page protections restored.
                Write(data + 8, [action == "disable" ? (byte)0 : (byte)1]);
            }
            if (!Read(data, 8).SequenceEqual(FocusMagic)) throw new InvalidOperationException("Background focus control header changed.");
            var control = Read(data, 80);
            if (control[9] != 1 || control[10] != 1) throw new InvalidOperationException("Background focus installation did not complete; policy remains disabled. Restart Workbench after preserving work.");
            if (control[8] > 1) throw new InvalidOperationException("Invalid background focus control state.");
            if (action is "enable" or "disable")
            {
                control[8] = action == "enable" ? (byte)1 : (byte)0;
                Write(data + 8, [control[8]]);
            }
            return new
            {
                state = control[8] == 1 ? "enabled" : "disabled", processId = process.Id,
                suppressedCalls = FocusImports.Select((f, i) => new { api = f.Name, count = BitConverter.ToUInt64(control, 16 + i * 8) }).ToArray(),
                scope = "Qt Windows platform in this Workbench process until exit. Background activation/raising and explicit flashing are suppressed; foreground use passes through. Manual switching remains available. disable allows exceptions; enable restores suppression.",
                limits = "Before attachment, separate game clients, native non-Qt dialogs and OS-initiated activation are outside this policy. Background maximize/restore requests use non-activating normal display."
            };
        }
        finally { N.CloseHandle(hp); }
    }

    internal static byte[] FocusStub(int index, nint original, nint data, nint foreground, nint windowPid, int pid)
    {
        var e = new E();
        e.B(0x48, 0x81, 0xec, 0xa8, 0, 0, 0);
        e.B(0x48, 0x89, 0x4c, 0x24, 0x20, 0x48, 0x89, 0x54, 0x24, 0x28,
            0x4c, 0x89, 0x44, 0x24, 0x30, 0x4c, 0x89, 0x4c, 0x24, 0x38);
        e.B(0x48, 0xb8); e.Q(data);
        e.B(0x80, 0x78, 8, 0); e.J(0x84, "pass");
        if (index == 5)
        {
            // FLASHW_STOP must still clear an already active flash.
            e.B(0x83, 0x79, 16, 0); e.J(0x84, "pass");
        }
        else
        {
            e.B(0x48, 0xb8); e.Q(foreground); e.B(0xff, 0xd0);
            e.B(0x48, 0x89, 0xc1, 0x48, 0x8d, 0x54, 0x24, 0x40,
                0xc7, 0x44, 0x24, 0x40, 0, 0, 0, 0);
            e.B(0x48, 0xb8); e.Q(windowPid); e.B(0xff, 0xd0);
            e.B(0x81, 0x7c, 0x24, 0x40); e.B(BitConverter.GetBytes(pid)); e.J(0x84, "pass");
        }
        e.B(0x48, 0xb8); e.Q(data + 16 + index * 8); e.B(0xf0, 0x48, 0xff, 0x00);
        if (index is 0 or 1 or 2 or 3 or 5)
        {
            e.B(0xb8, (byte)(index == 3 ? 1 : 0), 0, 0, 0);
            e.B(0x48, 0x81, 0xc4, 0xa8, 0, 0, 0, 0xc3);
        }
        else if (index == 7)
        {
            // Original stack arg 7: flags. Preserve geometry, prevent activation/raising.
            e.B(0x83, 0x8c, 0x24, 0xe0, 0, 0, 0, 0x14); e.J(0x84, "pass", true);
        }
        else
        {
            e.B(0x8b, 0x54, 0x24, 0x28); // ShowWindow command
            if (index == 4)
            {
                // WINDOWPLACEMENT is 44 bytes on Win64. Copy; never modify caller storage.
                e.B(0x48, 0x8b, 0x54, 0x24, 0x28,
                    0xf3, 0x0f, 0x6f, 0x02, 0xf3, 0x0f, 0x7f, 0x44, 0x24, 0x50,
                    0xf3, 0x0f, 0x6f, 0x42, 16, 0xf3, 0x0f, 0x7f, 0x44, 0x24, 0x60,
                    0x48, 0x8b, 0x42, 32, 0x48, 0x89, 0x44, 0x24, 0x70,
                    0x8b, 0x42, 40, 0x89, 0x44, 0x24, 0x78, 0x8b, 0x52, 8);
            }
            foreach (var command in new byte[] { 1, 3, 5, 9, 10 }) { e.B(0x83, 0xfa, command); e.J(0x84, "show"); }
            foreach (var command in new byte[] { 2, 6, 11 }) { e.B(0x83, 0xfa, command); e.J(0x84, "minimize"); }
            e.J(0x84, "mapped", true);
            e.Label("show"); e.B(0xba, 4, 0, 0, 0); e.J(0x84, "mapped", true);
            e.Label("minimize"); e.B(0xba, 7, 0, 0, 0);
            e.Label("mapped");
            if (index == 4)
            {
                e.B(0x89, 0x54, 0x24, 0x58, 0x48, 0x8d, 0x54, 0x24, 0x50, 0x48, 0x8b, 0x4c, 0x24, 0x20);
                e.B(0x48, 0xb8); e.Q(original); e.B(0xff, 0xd0);
                e.B(0x48, 0x81, 0xc4, 0xa8, 0, 0, 0, 0xc3);
            }
            else { e.B(0x89, 0x54, 0x24, 0x28); e.J(0x84, "pass", true); }
        }
        e.Label("pass");
        e.B(0x48, 0x8b, 0x4c, 0x24, 0x20, 0x48, 0x8b, 0x54, 0x24, 0x28,
            0x4c, 0x8b, 0x44, 0x24, 0x30, 0x4c, 0x8b, 0x4c, 0x24, 0x38);
        e.B(0x48, 0x81, 0xc4, 0xa8, 0, 0, 0);
        e.B(0xff, 0x25); e.B(BitConverter.GetBytes(16 - (64 + e.Count + 4)));
        var result = new byte[FocusStride];
        new byte[] { 0xe9, 59, 0, 0, 0 }.CopyTo(result, 0);
        FocusMagic.CopyTo(result, 8); BitConverter.GetBytes(original.ToInt64()).CopyTo(result, 16);
        BitConverter.GetBytes(data.ToInt64()).CopyTo(result, 24);
        e.Done().CopyTo(result, 64);
        return result;
    }
}

