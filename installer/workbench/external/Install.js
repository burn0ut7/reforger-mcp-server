// Leave Workbench free to service the installer's setup/reload requests.
var shell = new ActiveXObject("WScript.Shell");
var files = new ActiveXObject("Scripting.FileSystemObject");
var script = files.BuildPath(files.GetParentFolderName(WScript.ScriptFullName), "Install.ps1");
var executable = files.BuildPath(files.GetSpecialFolder(1), "WindowsPowerShell\\v1.0\\powershell.exe");
shell.Environment("Process")("GC_WORKBENCH_INSTALL_SCRIPT") = script;
shell.Environment("Process")("GC_WORKBENCH_PACKAGE") = files.GetParentFolderName(WScript.ScriptFullName);
var tools = WScript.Arguments.length >= 1 && WScript.Arguments(0) === "--tools";
// Opening the tools is not an install operation. Launch the registered app directly.
if (tools) {
    for (var argument = 1; argument < WScript.Arguments.length; argument += 2) {
        var key = WScript.Arguments(argument);
        if (!/^-(Lobby2|GlobalConflictsCore|GlobalConflictsUnits|GlobalConflictsMissions)$/.test(key) || argument + 1 >= WScript.Arguments.length) throw new Error("Invalid mod path argument.");
        shell.Environment("Process")("GC_WORKBENCH_MOD_" + key.substring(1)) = WScript.Arguments(argument + 1);
    }
    var registration = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%") + "\\GCWorkbench\\ui-path.txt";
    if (files.FileExists(registration)) {
        var reader = files.OpenTextFile(registration, 1, false, -1);
        var ui = reader.ReadAll().replace(/^\s+|\s+$/g, "");
        reader.Close();
        if (!files.FileExists(ui) || ui.indexOf('"') !== -1) {
            shell.Popup("GC Tools installation is incomplete. Run external\\workbench\\Install.js to repair it.", 0, "GC Tools", 16);
            WScript.Quit(1);
        }
        WScript.Quit(shell.Run('"' + ui + '" --tools', 1, true));
    }
}
shell.Environment("Process")("GC_WORKBENCH_PREVIEW") = tools ? " -Tools" : WScript.Arguments.length === 1 && WScript.Arguments(0) === "--preview" ? " -Preview" : "";
var bootstrap = "$start = New-Object Diagnostics.ProcessStartInfo; " +
    "$start.FileName = [Diagnostics.Process]::GetCurrentProcess().MainModule.FileName; " +
    "$start.Arguments = '-NoProfile -STA -ExecutionPolicy Bypass -File ' + [char]34 + $env:GC_WORKBENCH_INSTALL_SCRIPT + [char]34 + $env:GC_WORKBENCH_PREVIEW; " +
    "$start.UseShellExecute = $false; $start.CreateNoWindow = $true; " +
    "$child = [Diagnostics.Process]::Start($start); " +
    (tools ? "$child.WaitForExit(); exit $child.ExitCode" : "");
WScript.Quit(shell.Run('"' + executable + '" -NoProfile -NonInteractive -Command "' + bootstrap + '"', 0, true));
