using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ReforgerMcp;

internal static class RuntimeAdapter
{
    internal static readonly byte[] Source = ReadSource();
    internal static readonly string Build = Convert.ToHexString(SHA256.HashData(Source));
    internal static readonly byte[] InstalledSource = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Source).Replace("__RST_ADAPTER_BUILD__", Build, StringComparison.Ordinal));
    internal static readonly string InstalledHash = Convert.ToHexString(SHA256.HashData(InstalledSource));
    private static byte[] ReadSource()
    {
        using var input = typeof(RuntimeAdapter).Assembly.GetManifestResourceStream("ReforgerMcp.RuntimeAdapter")
            ?? throw new InvalidOperationException("Server distribution is missing its embedded runtime adapter.");
        using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray();
    }
    internal static string PathIn(string profile) => Path.Combine(profile, "scripts", "WorkbenchGame", "ReforgerMcp", "RST_Runtime.c");
    internal static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    internal static void CheckPath(string path)
    {
        for (var item = new DirectoryInfo(Path.GetDirectoryName(path)!); item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Adapter installation does not traverse junctions/symlinks: " + item.FullName);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Adapter target is a link: " + path);
    }
    internal static bool Install(string profile)
    {
        var target = PathIn(profile); CheckPath(target);
        var metadata = Path.Combine(profile, "ReforgerMcp", "runtime-install.json"); CheckPath(metadata);
        var previous = File.Exists(target) ? Hash(target) : null;
        if (previous == InstalledHash)
        {
            WriteManifest(metadata);
            return false;
        }
        if (previous is not null)
        {
            if (!File.Exists(metadata)) throw new InvalidOperationException("Unmanaged adapter preserved: " + target);
            using var manifest = JsonDocument.Parse(File.ReadAllText(metadata));
            if (manifest.RootElement.GetProperty("installedSha256").GetString() != previous)
                throw new InvalidOperationException("Adapter has local edits; preserved without overwrite: " + target);
            var backup = Path.Combine(profile, "ReforgerMcp", "backups", previous + ".c"); CheckPath(backup);
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            if (!File.Exists(backup)) File.Copy(target, backup);
            else if (Hash(backup) != previous) throw new InvalidOperationException("Adapter backup conflict: " + backup);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.CreateDirectory(Path.GetDirectoryName(metadata)!);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, InstalledSource);
            if ((File.Exists(target) ? Hash(target) : null) != previous) throw new InvalidOperationException("Adapter changed during installation.");
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        WriteManifest(metadata);
        return true;
    }
    private static void WriteManifest(string metadata)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(metadata)!);
        var temporary = metadata + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { adapterBuild = Build, installedSha256 = InstalledHash }, Configuration.Json));
            File.Move(temporary, metadata, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static JsonElement Handshake(int port, string profile)
    {
        var receipt = "r" + Guid.NewGuid().ToString("N");
        var response = RuntimeNet.Call(port, new { APIFunc = "RST_Runtime", protocol = 2, action = "status", token = receipt });
        if (!response.TryGetProperty("protocol", out var protocol) || protocol.GetInt32() != 2 ||
            !response.TryGetProperty("token", out var token) || token.GetString() != receipt ||
            !response.TryGetProperty("adapterBuild", out var build) || build.GetString() != Build ||
            !response.TryGetProperty("profileDirectory", out var path) || !string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.GetString()!)), profile, StringComparison.OrdinalIgnoreCase) ||
            !response.TryGetProperty("state", out var state) || state.GetString() is not ("playing" or "not_playing"))
            throw new InvalidOperationException("Runtime adapter identity/profile/protocol mismatch. Install this server's adapter and inspect reload diagnostics.");
        return response;
    }
}

public sealed partial class WorkbenchSession
{
    internal Task<object> RuntimeSetup(string action, int processId, CancellationToken ct)
    {
        if (action is not ("status" or "install" or "enable")) throw new ArgumentException("action must be status, install or enable.");
        return Start(token =>
        {
            using var process = SelectProcess();
            if (process.Id != processId) throw new InvalidOperationException("Workbench process changed.");
            var observedWindows = Windows(process.Id);
            RequiredWindow(observedWindows, ResourceManager);
            if (observedWindows.Any(IsModal) || observedWindows.Any(w => !w.Current.IsEnabled))
                throw Blocked(process, "Resolve Workbench dialogs before runtime setup.");
            if (action == "install" && (observedWindows.Any(w => w.Current.AutomationId is not (ResourceManager or ScriptEditor or "Arma4WorkbenchApp.NotificationPopup")) ||
                observedWindows.Any(w => w.FindAll(System.Windows.Automation.TreeScope.Descendants,
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.TabItem))
                    .Cast<System.Windows.Automation.AutomationElement>().Any(t => t.Current.Name.Contains('*')))))
                throw Blocked(process, "Close World Editor/other editors and save script tabs before installing the profile bridge. Setup reloads scripts.");
            var native = (NativeRuntimeSetup)NativeWorkbench.Execute(process, token, runtimeSetup: false);
            var target = RuntimeAdapter.PathIn(native.ProfileDirectory);
            RuntimeAdapter.CheckPath(target);
            var installed = File.Exists(target) && RuntimeAdapter.Hash(target) == RuntimeAdapter.InstalledHash;
            var configuredPort = configuration.Workbench!.RuntimePort;
            if (action != "status" && configuredPort != native.Port)
                throw new InvalidOperationException($"Set workbench.runtimePort to Workbench's current NET API port {native.Port}. No port/settings/files were changed.");
            var changed = false;
            var wasEnabled = native.Enabled;
            if (action == "install") changed = RuntimeAdapter.Install(native.ProfileDirectory);
            if (action != "status") native = (NativeRuntimeSetup)NativeWorkbench.Execute(process, token, runtimeSetup: true);
            var needsReload = action == "install";
            if (needsReload && !changed)
            {
                try
                {
                    RuntimeNet.VerifyListener(native.Port, process.Id);
                    RuntimeAdapter.Handshake(native.Port, native.ProfileDirectory);
                    needsReload = false;
                }
                catch (InvalidOperationException) { /* Current source is present but not confirmed loaded; explicit install reloads it. */ }
            }
            if (needsReload)
            {
                runtimePrepared = null;
                var reload = JsonSerializer.SerializeToElement(ExecuteCore(processId, token), Configuration.Json);
                if (!reload.TryGetProperty("state", out var reloadState) || reloadState.GetString() != "reloaded")
                    return new { state = "reload_failed", profileDirectory = native.ProfileDirectory, adapterPath = target, changed, reload,
                        message = "Profile adapter remains installed; readiness is unverified. Resolve diagnostics before another explicit operation." };
                installed = File.Exists(target) && RuntimeAdapter.Hash(target) == RuntimeAdapter.InstalledHash;
            }
            var listener = false; JsonElement? builtin = null, adapter = null; string? problem = null;
            try
            {
                var until = DateTime.UtcNow.AddSeconds(action == "status" ? 0 : 5);
                while (true)
                {
                    try { RuntimeNet.VerifyListener(native.Port, process.Id); listener = true; break; }
                    catch (InvalidOperationException) when (DateTime.UtcNow < until) { Thread.Sleep(100); }
                }
                builtin = RuntimeNet.Call(native.Port, new { APIFunc = "IsWorkbenchRunning" });
                if (!builtin.Value.GetProperty("IsRunning").GetBoolean() || !builtin.Value.GetProperty("ScriptsCompiled").GetBoolean())
                    throw new InvalidOperationException("Workbench is not running with successfully compiled scripts.");
                if (!installed) throw new InvalidOperationException("Current profile adapter is missing or differs from this server. Run install.");
                adapter = RuntimeAdapter.Handshake(native.Port, native.ProfileDirectory);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { problem = ex.Message; }
            var ready = native.Enabled && installed && configuredPort == native.Port && adapter is not null;
            return new { state = ready ? "ready" : "not_ready", processId, profileDirectory = native.ProfileDirectory,
                adapterPath = target, adapterBuild = RuntimeAdapter.Build, installedSha256 = RuntimeAdapter.InstalledHash,
                changed, reloaded = needsReload, installed, wasEnabled, enabled = native.Enabled, port = native.Port, configuredPort, listener, builtin, adapter, problem,
                scope = "Readiness requires the current profile file, enabled engine setting, selected-process listener, successful compilation and matching live adapter handshake. NET API may listen on all interfaces; client requests use loopback. No alternate port or executor." };
        }, ct);
    }
}

[McpServerToolType]
public sealed class WorkbenchRuntimeSetupTools(WorkbenchSession session)
{
    [McpServerTool(Name = "workbench_runtime_setup", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Explicit runtime bridge setup for the running supported Workbench. action=status observes the mounted profile, NET API setting/port and verifies the installed/live adapter. action=enable enables and persists NET API through its native owner even when NET API is off; preserves the port and unrelated settings. action=install also installs this server's embedded adapter into profile/scripts/WorkbenchGame/ReforgerMcp and reloads scripts; close World Editor/other editors and save script tabs first. Locally modified/unmanaged files are preserved. Requires workbench.runtimePort to match the observed editor port for mutations. Never installs in gameplay addons. ready requires a PID-owned listener and matching adapter build/profile/protocol. Poll pending via workbench_operation; never blindly retry after uncertainty.")]
    public async Task<object> Setup(string action, int processId, CancellationToken cancellationToken = default)
    {
        try { return await session.RuntimeSetup(action, processId, cancellationToken); }
        catch (Exception ex) when (ex is not (McpException or OperationCanceledException)) { throw new McpException(ex.Message); }
    }
}
