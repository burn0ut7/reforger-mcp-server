using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GCWorkbench.UI;

internal static class ToolStatus
{
    private static readonly string Control = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GCWorkbench");

    internal static async Task<DialogSpec> ModsAsync(CancellationToken cancellation)
    {
        var script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "lobby2-tools", "GC-ModStatus.ps1"));
        if (!File.Exists(script)) return ToolPages.Updates("Install the current GC Tools package");
        var keys = new[] { "Lobby2", "GlobalConflictsCore", "GlobalConflictsUnits", "GlobalConflictsMissions" };
        var arguments = new List<string> { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script };
        foreach (var key in keys)
        {
            var project = Environment.GetEnvironmentVariable("GC_WORKBENCH_MOD_" + key);
            if (string.IsNullOrEmpty(project) && key == "Lobby2" && Environment.GetEnvironmentVariable("GC_WORKBENCH_PACKAGE") is { } package)
                project = Path.GetFullPath(Path.Combine(package, "..", "..", "addon.gproj"));
            if (!string.IsNullOrEmpty(project)) arguments.AddRange(["-" + key, project]);
        }
        var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var page = ToolPages.Updates();
        var fields = await Task.WhenAll(page.Fields.Select(async (field, index) =>
        {
            var result = await RunAsync(powershell, [.. arguments, "-Mod", keys[index]], 20, cancellation);
            return ModStatusField(field, result.Code, result.Output);
        }));
        return page with { Fields = fields.ToList() };
    }

    internal static InputField ModStatusField(InputField field, int code, string output)
    {
        try
        {
            if (code != 0) throw new JsonException();
            using var json = JsonDocument.Parse(output);
            var needed = json.RootElement.GetProperty("UpdateNeeded").GetBoolean();
            var status = json.RootElement.GetProperty("Status").GetString() ?? "Unable to check - refresh";
            return field with { Enabled = needed, Checked = needed, Help = status };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        { return field with { Enabled = false, Checked = false, Help = "Unable to check - refresh" }; }
    }

    internal static async Task<DialogSpec> GitHubAsync(bool signIn, CancellationToken cancellation)
    {
        var script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "lobby2-tools", "GC-GitHub.ps1"));
        if (!File.Exists(script)) return ToolPages.GitHub() with { Items = [new("Connection", "", "Install the current GC Tools package", "warning")] };
        var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var result = await RunAsync(powershell, ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Action", signIn ? "SignIn" : "Check"], signIn ? 180 : 30, cancellation);
        if (result.Code != 0) return ToolPages.GitHub() with { Items = [new("Connection", "", signIn ? "Sign-in cancelled or unavailable" : "Could not check connection", "warning")] };
        try
        {
            using var json = JsonDocument.Parse(result.Output);
            var status = json.RootElement;
            var connected = status.GetProperty("Connected").GetBoolean();
            var access = status.GetProperty("Repository").GetString() ?? "Not checked";
            return ToolPages.GitHub() with
            {
                PrimaryAction = connected ? "Switch GitHub account" : "Sign in on GitHub",
                Items = [new("GitHub", "", status.GetProperty("Status").GetString() ?? "Not checked", connected ? "ready" : "warning"),
                    new("Mission repository", "", access, access == "Accessible" ? "ready" : "warning")]
            };
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        { return ToolPages.GitHub() with { Items = [new("Connection", "", "Could not check connection", "warning")] }; }
    }

    internal static async Task<DialogSpec> DependenciesAsync(CancellationToken cancellation)
    {
        var tools = Task.Run(() => CheckWorkbench(), cancellation);
        var git = CheckGitAsync(cancellation);
        await Task.WhenAll(tools, git);
        return ToolPages.Dependencies() with { Section = "", Items = [await tools, await git] };
    }

    internal static StatusItem CheckWorkbench(string? controlDirectory = null, string? packageDirectory = null, string? uiDirectory = null)
    {
        try
        {
            var installation = Path.Combine(controlDirectory ?? Control, "installation.json");
            if (!File.Exists(installation)) return new("GC Workbench Tools", "", "Not installed", "warning");
            using var installed = JsonDocument.Parse(File.ReadAllText(installation));
            var destination = installed.RootElement.GetProperty("destination").GetString()!;
            var profile = installed.RootElement.GetProperty("profileDirectory").GetString()!;
            if (!File.Exists(Path.Combine(destination, "payload", "generic", "rstauthoring.dll"))) return new("GC Workbench Tools", "", "Installation incomplete", "warning");
            var package = packageDirectory ?? Environment.GetEnvironmentVariable("GC_WORKBENCH_PACKAGE");
            if (string.IsNullOrEmpty(package)) return new("GC Workbench Tools", "", "Installed \u00b7 update check unavailable", "warning");
            var zipPath = Path.Combine(package, "GCWorkbench.zip");
            var expected = File.ReadAllText(zipPath + ".sha256").Trim().Split(' ')[0];
            using (var bytes = File.OpenRead(zipPath))
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    return new("GC Workbench Tools", "", "Dependency package needs repair", "warning");
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.GetEntry("support/files.json") ?? throw new IOException("No manifest");
            using var manifest = JsonDocument.Parse(entry.Open());
            var checkedFiles = 0;
            foreach (var file in manifest.RootElement.EnumerateArray())
            {
                var relative = file.GetProperty("path").GetString()!;
                string target;
                if (relative.StartsWith("support/payload/lobby2-tools/", StringComparison.Ordinal))
                    target = Path.Combine(profile, "GCWorkbench", "tools", Path.GetFileName(relative));
                else if (relative.StartsWith("support/payload/helper/", StringComparison.Ordinal))
                    target = Path.GetFileName(relative).StartsWith("GCWorkbench.UI", StringComparison.Ordinal)
                        ? Path.Combine(uiDirectory ?? AppContext.BaseDirectory, Path.GetFileName(relative))
                        : Path.Combine(destination, relative["support/".Length..]);
                else if (relative.StartsWith("support/payload/generic/", StringComparison.Ordinal))
                    target = Path.Combine(destination, relative["support/".Length..]);
                else continue;
                checkedFiles++;
                if (!File.Exists(target)) return new("GC Workbench Tools", "", "Installation incomplete \u00b7 install/update required", "warning");
                using var content = File.OpenRead(target);
                if (!Convert.ToHexString(SHA256.HashData(content)).Equals(file.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                    return new("GC Workbench Tools", "", "Update or repair available", "warning");
            }
            return checkedFiles == 0 ? new("GC Workbench Tools", "", "Update check unavailable", "warning")
                : new("GC Workbench Tools", "Lobby package", "Up to date", "ready");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException)
        { return new("GC Workbench Tools", "", "Could not verify installation", "warning"); }
    }

    internal static Version? ParseGitVersion(string value)
    {
        var match = Regex.Match(value, @"(?:git version |v)?(\d+)\.(\d+)\.(\d+)(?:\.windows\.(\d+))?");
        return match.Success ? new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0) : null;
    }

    private static async Task<StatusItem> CheckGitAsync(CancellationToken cancellation)
    {
        var result = await RunAsync("git.exe", ["--version"], 5, cancellation);
        var current = ParseGitVersion(result.Output);
        if (result.Code != 0 || current is null) return new("Git for Windows", "", "Not installed or unavailable", "warning");
        var manager = await RunAsync("git.exe", ["credential-manager", "--version"], 5, cancellation);
        if (manager.Code != 0) return new("Git for Windows", result.Output.Trim(), "Git Credential Manager missing", "warning");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GC-Workbench");
            using var response = await client.GetAsync("https://api.github.com/repos/git-for-windows/git/releases/latest", cancellation);
            response.EnsureSuccessStatusCode();
            using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
            var latest = ParseGitVersion(release.RootElement.GetProperty("tag_name").GetString() ?? "");
            if (latest is null) throw new JsonException();
            return new("Git for Windows", result.Output.Trim().Replace("git version ", ""), current < latest ? "Update available" : "Up to date", current < latest ? "warning" : "ready");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        { return new("Git for Windows", result.Output.Trim().Replace("git version ", ""), "Installed \u00b7 update check unavailable", "warning"); }
    }

    internal static async Task<(int Code, string Output)> RunAsync(string executable, string[] arguments, int seconds, CancellationToken cancellation)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(seconds));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["GCM_TRACE_SECRETS"] = "0";
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) return (-1, "");
            var output = process.StandardOutput.ReadToEndAsync(limit.Token);
            var error = process.StandardError.ReadToEndAsync(limit.Token);
            await process.WaitForExitAsync(limit.Token);
            await error;
            return (process.ExitCode, await output);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or OperationCanceledException or IOException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return (-1, "");
        }
    }
}
