$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../GC-ModStatus.ps1')
function Assert($value,$why){if(!$value){throw $why}}
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('gc-mod-status-'+[guid]::NewGuid())
$upstream=Join-Path $fixture 'upstream';$local=Join-Path $fixture 'local'
$null=New-Item -ItemType Directory -Path $upstream -Force
$null=Git $upstream @('init','-q','-b','main')
$null=Git $upstream @('config','user.name','Test');$null=Git $upstream @('config','user.email','test@example.invalid')
[IO.File]::WriteAllText("$upstream/addon.gproj",' GUID "6A115D5DFF7ED870"')
[IO.File]::WriteAllText("$upstream/file",'base')
$null=Git $upstream @('add','.');$null=Git $upstream @('commit','-qm','Base')
$null=Git $upstream @('clone','-q',$upstream,$local)
$null=Git $local @('config',"url.$upstream.insteadOf",'https://github.com/fixture/mod.git')
$null=Git $local @('config','user.name','Test');$null=Git $local @('config','user.email','test@example.invalid')
$addon=@{Name='Fixture';Key='Lobby2';Project="$local/addon.gproj";Guid='6A115D5DFF7ED870';Repo='fixture/mod'}
Assert ((Get-GcModStatus $addon).Status -eq 'Up to date') 'Equal tips not current'
[IO.File]::WriteAllText("$local/private",'my changes');$null=Git $local @('add','private');$null=Git $local @('commit','-qm','Personal changes')
Assert (!(Get-GcModStatus $addon).UpdateNeeded) 'Local-only commits offered as update'
[IO.File]::WriteAllText("$upstream/file",'new shared');$null=Git $upstream @('add','.');$null=Git $upstream @('commit','-qm','Shared changes')
Assert ((Get-GcModStatus $addon).Status -eq 'Local changes need review') 'Diverged default branch offered unsafe update'
$null=Git $local @('switch','-qc','mission/example')
Assert ((Get-GcModStatus $addon).UpdateNeeded -and (Get-GcModStatus $addon).Status -like '*review may be needed') 'Personal branch update not offered'
$behind=Join-Path $fixture 'behind';$null=Git $upstream @('clone','-q',$upstream,$behind)
$null=Git $behind @('config',"url.$upstream.insteadOf",'https://github.com/fixture/mod.git')
$addon.Project="$behind/addon.gproj"
[IO.File]::WriteAllText("$behind/private",'staged content');$null=Git $behind @('add','private');[IO.File]::WriteAllText("$behind/private",'working content')
$head=Git $behind @('rev-parse','HEAD');$index=(Get-FileHash "$behind/.git/index").Hash
[IO.File]::WriteAllText("$upstream/file",'newer');$null=Git $upstream @('add','.');$null=Git $upstream @('commit','-qm','New shared update')
$status=Get-GcModStatus $addon
Assert ($status.UpdateNeeded -and $status.Status -eq 'Update available') 'Behind not offered'
Assert ((Git $behind @('rev-parse','HEAD')) -eq $head -and (Get-FileHash "$behind/.git/index").Hash -eq $index -and [IO.File]::ReadAllText("$behind/private") -eq 'working content') 'Status mutated working checkout'
Assert (!(Test-Path "$behind/.git/FETCH_HEAD")) 'Status changed fetch metadata'
$addon.Project=Join-Path $fixture 'missing/addon.gproj';Assert (!(Get-GcModStatus $addon).UpdateNeeded -and (Get-GcModStatus $addon).Status -eq 'Mod folder not found') 'Missing checkout enabled'
$addon.Project="$behind/addon.gproj"
function Get-ModBase {throw 'offline'}
Assert ((Get-GcModStatus $addon).Status -eq 'Unable to check - refresh' -and !(Get-GcModStatus $addon).UpdateNeeded) 'Failed check claimed current'
'PASS: current, locally ahead, behind, diverged main/personal, missing/offline, and unchanged HEAD/index/files/FETCH_HEAD.'
