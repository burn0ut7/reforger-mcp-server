using System.ComponentModel;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

public sealed partial class WorkbenchSession
{
    internal Task<object> LoadExtension(int processId, string filePath, string expectedSha256, string specification, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(filePath) || !File.Exists(filePath) || !Path.GetExtension(filePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose an existing absolute extension DLL path.");
        if (expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("Supply the SHA256 of the deliberately selected build.");
        var path = Path.GetFullPath(filePath);
        var command = new NativeWorkbench.ExtensionCommand(path, specification);
        _ = NativeWorkbench.ExtensionInput(command);
        return Start(token =>
        {
            using var process = SelectProcess();
            if (process.Id != processId) throw new InvalidOperationException("Workbench process changed; inspect workbench_status.");
            var windows = Windows(process.Id);
            RequiredWindow(windows, ResourceManager);
            if (windows.Any(IsModal) || windows.Any(w => !w.Current.IsEnabled))
                throw Blocked(process, "Resolve editor dialogs before loading the extension.");
            if (process.Modules.Cast<System.Diagnostics.ProcessModule>().Any(m => Path.GetFileName(m.FileName).Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A DLL with this name is already resident. Inspect its original outcome; changing builds requires a preserved-work Workbench restart, not a second load.");
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = Convert.ToHexString(SHA256.HashData(file));
            if (!hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Extension DLL changed; no code loaded.");
            file.Position = 0;
            using var pe = new PEReader(file, PEStreamOptions.LeaveOpen);
            if (pe.PEHeaders.CoffHeader.Machine != Machine.Amd64 || !pe.PEHeaders.CoffHeader.Characteristics.HasFlag(Characteristics.Dll))
                throw new InvalidOperationException("Extension must be an x64 Windows DLL.");
            var result = (NativeWorkbench.ExtensionResult)NativeWorkbench.Execute(process, token, extension: command);
            process.Refresh();
            var observed = process.Modules.Cast<System.Diagnostics.ProcessModule>().Any(m => Path.GetFullPath(m.FileName).Equals(path, StringComparison.OrdinalIgnoreCase));
            return new { filePath = path, sha256 = hash, selectedModuleObserved = observed, result,
                scope = "Executed the selected native DLL and its dependencies in Workbench. Calls RST_WorkbenchExtensionStart_v1 on the editor thread. Inherits the normal MCP dispatch policy enabling process-local Yes to All for protected script methods. Module stays resident; no automatic unload or replay. Entry return is not verification of authoring behavior; inspect extension-specific evidence." };
        }, ct);
    }
}

[McpServerToolType]
public sealed class WorkbenchExtensionTools(WorkbenchSession session)
{
    [McpServerTool(Name = "workbench_load_extension", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Load an explicitly selected absolute x64 DLL with expectedSha256 and call its RST_WorkbenchExtensionStart_v1(wchar_t*) entry on the Workbench editor thread. Executes native code/dependencies and inherits MCP's process-local Yes to All script-authorization policy. specification is passed verbatim (up to 767 UTF-16 units). Refuses an already-resident DLL name. No automatic unload/retry; restart Workbench preserving authored work to change builds. Poll workbench_operation and verify extension evidence separately.")]
    public Task<object> Load(int processId, string filePath, string expectedSha256, string specification = "", CancellationToken cancellationToken = default)
        => session.LoadExtension(processId, filePath, expectedSha256, specification, cancellationToken);
}
