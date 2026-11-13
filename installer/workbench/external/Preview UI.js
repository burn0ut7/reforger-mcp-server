var shell = new ActiveXObject("WScript.Shell");
var files = new ActiveXObject("Scripting.FileSystemObject");
var launcher = files.BuildPath(files.GetParentFolderName(WScript.ScriptFullName), "Install.js");
shell.Run('wscript.exe //nologo //E:JScript "' + launcher + '" --preview', 0, false);
