using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using GCWorkbench.UI;
var root=Path.Combine(Path.GetTempPath(),"gc-status-"+Guid.NewGuid());
var control=Path.Combine(root,"control");var install=Path.Combine(root,"installed");var profile=Path.Combine(root,"profile");var package=Path.Combine(root,"package");var ui=Path.Combine(root,"ui");
foreach(var folder in new[]{control,install,profile,package,ui})Directory.CreateDirectory(folder);
void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
Check(ToolStatus.CheckWorkbench(control,package,ui).Status=="Not installed","Missing installation");
File.WriteAllText(Path.Combine(control,"installation.json"),JsonSerializer.Serialize(new{destination=install,profileDirectory=profile}));
Check(ToolStatus.CheckWorkbench(control,package,ui).Status=="Installation incomplete","Missing native DLL");
var paths=new Dictionary<string,string>{["support/payload/generic/rstauthoring.dll"]=Path.Combine(install,"payload/generic/rstauthoring.dll"),["support/payload/helper/GCWorkbench.UI.dll"]=Path.Combine(ui,"GCWorkbench.UI.dll"),["support/payload/lobby2-tools/GC-GitHub.ps1"]=Path.Combine(profile,"GCWorkbench/tools/GC-GitHub.ps1")};
foreach(var target in paths.Values){Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.WriteAllText(target,"expected");}
var zipPath=Path.Combine(package,"GCWorkbench.zip");
using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry("support/files.json").Open()))writer.Write(JsonSerializer.Serialize(paths.Select(p=>new{path=p.Key,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p.Value)))})));
File.WriteAllText(zipPath+".sha256",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zipPath)))+"  GCWorkbench.zip");
Check(ToolStatus.CheckWorkbench(control,package,ui).Status=="Up to date","Matching package");
File.WriteAllText(paths.Values.Last(),"old helper");
Check(ToolStatus.CheckWorkbench(control,package,ui).Status=="Update or repair available","Changed helper");
File.Delete(paths.Values.Last());
Check(ToolStatus.CheckWorkbench(control,package,ui).Status.StartsWith("Installation incomplete"),"Missing helper");
File.WriteAllText(zipPath+".sha256",new string('0',64));
Check(ToolStatus.CheckWorkbench(control,package,ui).Status=="Dependency package needs repair","Corrupt package");
Check(ToolStatus.ParseGitVersion("git version 2.54.0.windows.1")!>ToolStatus.ParseGitVersion("v2.54.0.windows.0"),"Windows revision comparison");
Check(ToolStatus.ParseGitVersion("v2.55.0.windows.1")!>ToolStatus.ParseGitVersion("git version 2.54.9.windows.9"),"New version comparison");
Check(ToolStatus.ParseGitVersion("unavailable")==null,"Invalid version");
var missing=await ToolStatus.RunAsync(Path.Combine(root,"absent.exe"),[],1,CancellationToken.None);
Check(missing.Code==-1,"Missing process not contained");
using(var cancelled=new CancellationTokenSource()) {
 cancelled.Cancel();
 var stopped=await ToolStatus.RunAsync("git.exe",["--version"],1,cancelled.Token);
 Check(stopped.Code==-1&&stopped.Output=="","Queued cancellation escaped status contract");
}
var slow=await ToolStatus.RunAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell/v1.0/powershell.exe"),["-NoProfile","-NonInteractive","-Command","Start-Sleep -Seconds 10"],1,CancellationToken.None);
Check(slow.Code==-1,"Slow check did not time out safely");
Console.WriteLine("PASS: missing/incomplete/current/outdated/corrupt installation, Git version comparison, unavailable helper.");
var currentMod=ToolStatus.ModStatusField(new InputField{Id="lobby",Label="Lobby2",Kind="check",Checked=true},0,"{\"Status\":\"Up to date\",\"UpdateNeeded\":false}");
Check(!currentMod.Enabled&&!currentMod.Checked,"Current mod eligibility");
var oldMod=ToolStatus.ModStatusField(currentMod,0,"{\"Status\":\"Update available\",\"UpdateNeeded\":true}");
Check(oldMod.Enabled&&oldMod.Checked,"Outdated mod eligibility");
Check(!ToolStatus.ModStatusField(oldMod,-1,"").Enabled,"Failed mod check eligibility");
Console.WriteLine("PASS: per-mod status eligibility mapping.");
// First-launch routing uses local presence, not network/update-check results.
Check(ToolPages.Initial(true).Title=="External dependencies","Missing dependency start page");
Check(ToolPages.Initial(false).Title=="Submit mission","Installed start page");
Check(ToolStatus.MissingStartupDependencies(control,""),"Missing Git or helpers not detected");
var helper=Path.Combine(install,"payload/helper/ReforgerMcp.exe");Directory.CreateDirectory(Path.GetDirectoryName(helper)!);File.WriteAllText(helper,"installed older version");
foreach(var name in new[]{"GC-ModLauncher.js","GC-ModWorkflow.ps1","GC-SubmitMission.ps1","GC-Review.ps1","GC-GitHub.ps1","GC-ModStatus.ps1"})File.WriteAllText(Path.Combine(profile,"GCWorkbench/tools",name),"installed");
var gitFolder=Path.Combine(root,"git bin");Directory.CreateDirectory(gitFolder);File.WriteAllText(Path.Combine(gitFolder,"git.exe"),"installed");
Check(!ToolStatus.MissingStartupDependencies(control,gitFolder),"Present components mistaken for missing because updates exist");
Check(ToolStatus.MissingStartupDependencies(control,""),"Missing Git not detected");
File.Delete(Path.Combine(profile,"GCWorkbench/tools/GC-ModStatus.ps1"));
Check(ToolStatus.MissingStartupDependencies(control,gitFolder),"Missing profile helper not detected");
File.WriteAllText(Path.Combine(control,"installation.json"),"broken record");
Check(ToolStatus.MissingStartupDependencies(control,gitFolder),"Broken install not routed to dependencies");
Console.WriteLine("PASS: first-launch routing, present/older components, missing Git/helper, damaged installation.");
