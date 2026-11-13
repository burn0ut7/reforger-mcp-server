// Leave Workbench free to service the installer's setup/reload requests.
var shell = new ActiveXObject("WScript.Shell");
var files = new ActiveXObject("Scripting.FileSystemObject");
var script = files.BuildPath(files.GetParentFolderName(WScript.ScriptFullName), "Install.ps1");
var executable = files.BuildPath(files.GetSpecialFolder(1), "WindowsPowerShell\\v1.0\\powershell.exe");
shell.Environment("Process")("GC_WORKBENCH_INSTALL_SCRIPT") = script;
var bootstrap = "$start = New-Object Diagnostics.ProcessStartInfo; " +
    "$start.FileName = [Diagnostics.Process]::GetCurrentProcess().MainModule.FileName; " +
    "$start.Arguments = '-NoProfile -STA -ExecutionPolicy Bypass -File ' + [char]34 + $env:GC_WORKBENCH_INSTALL_SCRIPT + [char]34; " +
    "$start.UseShellExecute = $false; $start.CreateNoWindow = $true; " +
    "[Diagnostics.Process]::Start($start) | Out-Null";
WScript.Quit(shell.Run('"' + executable + '" -NoProfile -NonInteractive -Command "' + bootstrap + '"', 0, true));
