// Use the Windows GUI script host so launching PowerShell never allocates a visible terminal.
var shell = new ActiveXObject("WScript.Shell");
var files = new ActiveXObject("Scripting.FileSystemObject");
var helper = files.BuildPath(files.GetParentFolderName(WScript.ScriptFullName), "GC-ModWorkflow.ps1");
var executable = files.BuildPath(files.GetSpecialFolder(1), "WindowsPowerShell\\v1.0\\powershell.exe");
function quote(value) {
    // Windows paths and the fixed option values cannot contain double quotes.
    if (value.indexOf('"') !== -1) throw new Error("Unexpected quote in argument.");
    return '"' + value + '"';
}
var args = "-NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File " + quote(helper);
for (var i = 0; i < WScript.Arguments.length; i++) args += " " + quote(WScript.Arguments(i));
// WScript's hidden window style suppresses GUI dialogs as well. Start the actual
// helper with CreateNoWindow so it has no console and can show ordinary dialogs.
shell.Environment("Process")("GC_WORKBENCH_GIT_ARGS") = args;
var bootstrap = "$ErrorActionPreference = 'Stop'; $start = New-Object Diagnostics.ProcessStartInfo; " +
    "$start.FileName = [Diagnostics.Process]::GetCurrentProcess().MainModule.FileName; " +
    "$start.Arguments = $env:GC_WORKBENCH_GIT_ARGS; $start.UseShellExecute = $false; " +
    "$start.CreateNoWindow = $true; $child = [Diagnostics.Process]::Start($start); " +
    "$child.WaitForExit(); exit $child.ExitCode";
var command = quote(executable) + " -NoProfile -NonInteractive -Command " + quote(bootstrap);
// Distinct exits let Workbench distinguish a completed workflow from a failed launch.
var result = shell.Run(command, 0, true);
WScript.Quit(result === 0 ? 73 : result === 2 ? 74 : 1);
