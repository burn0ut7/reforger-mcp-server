$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../GC-ModWorkflow.ps1')
$t=Join-Path $env:TEMP ('gc-safe-update-'+[guid]::NewGuid())
$s=Join-Path $t upstream
git.exe init -q -b main $s
git.exe -C $s config user.name Test
git.exe -C $s config user.email test@example.invalid
' GUID "6A115D5DFF7ED870"'|Set-Content "$s/addon.gproj"
foreach($f in 'incoming','mine','staged'){ 'base' | Set-Content "$s/$f" }
git.exe -C $s add .
git.exe -C $s commit -qm base
$w=Join-Path $t work
git.exe clone -q $s $w
git.exe -C $w config "url.$s.insteadOf" https://github.com/burn0ut7/Lobby2.git
$addon=@{Name='Lobby2';Project="$w/addon.gproj";Guid='6A115D5DFF7ED870';Repo='burn0ut7/Lobby2'}
function Confirm-Replacement($name,$paths){$script:asked++; $script:shown=($paths -join ','); return $script:accept}
function check($condition,$why){if(!$condition){throw $why}}
function publish($file,$content){$content|Set-Content "$s/$file";git.exe -C $s add -- $file;git.exe -C $s commit -qm update}
function update { $script:asked=0; $changed=$false; $pause=$false; $r=Update-Mod $addon ([ref]$changed) ([ref]$pause); Write-Host $r; return $r }
'personal'|Set-Content "$w/mine"; 'stage1'|Set-Content "$w/staged";git.exe -C $w add staged; 'stage2'|Set-Content "$w/staged"; 'new'|Set-Content "$w/loose"
publish incoming two
$r=update; check ($r -eq 'Updated.' -and !$asked -and (Get-Content "$w/incoming") -eq 'two' -and (git.exe -C $w show ':staged') -eq 'stage1' -and (Get-Content "$w/staged") -eq 'stage2' -and (Get-Content "$w/loose") -eq 'new') 'safe update'
$r=update;check ($r -eq 'Up to date.' -and !$asked) 'up to date'
'edit'|Set-Content "$w/incoming";publish incoming three
$accept=$false;$r=update;check ($asked -eq 1 -and $shown -eq 'incoming' -and (Get-Content "$w/incoming") -eq 'edit') 'skip'
$accept=$true;$r=update;check ($asked -eq 1 -and $r -like 'Updated. Your edits are in backup *' -and (Get-Content "$w/incoming") -eq 'three' -and (git.exe -C $w show ':staged') -eq 'stage1' -and (Get-Content "$w/mine") -eq 'personal' -and (Get-Content "$w/staged") -eq 'stage2') 'selective backup'
publish loose incomingfile
$r=update;check ($asked -eq 1 -and $shown -eq 'loose' -and (Get-Content "$w/loose") -eq 'incomingfile' -and (git.exe -C $w show 'stash@{0}^3:loose') -eq 'new') 'untracked backup'
'ignored'|Set-Content "$w/.git/info/exclude";'keep'|Set-Content "$w/ignored";publish ignored upstream
$r=update;check (!$asked -and (Get-Content "$w/ignored") -eq 'keep') 'ignored overwritten'
git.exe -C $w switch -qc my-mission;$r=update;check (!$asked -and $r -like 'Skipped*') 'ignored file protected on feature branch'
'PASS'

# Committed conflicts on a mission branch use the shared in-app review.
$conflictRoot=Join-Path $t conflict
$null=Git $s @('clone','-q',$s,$conflictRoot)
$null=Git $conflictRoot @('config',"url.$s.insteadOf",'https://github.com/burn0ut7/Lobby2.git')
$null=Git $conflictRoot @('config','user.name','Tester')
$null=Git $conflictRoot @('config','user.email','test@example.invalid')
$null=Git $conflictRoot @('switch','-qc','mission/example')
[IO.File]::WriteAllText("$conflictRoot/incoming",'mine')
$null=Git $conflictRoot @('add','incoming');$null=Git $conflictRoot @('commit','-qm','Mission edit')
$initial=Git $conflictRoot @('rev-parse','HEAD')
publish incoming 'shared conflict'
$addon.Project="$conflictRoot/addon.gproj"
$script:resolve=$false
function Show-GcReview($spec) {if($script:resolve){return @{Action='shared'}}}
$changed=$false;$pending=$false
$r=Update-Mod $addon ([ref]$changed) ([ref]$pending)
check ($r -like 'Cancelled*' -and !$changed -and (Git $conflictRoot @('rev-parse','HEAD')) -eq $initial) 'Conflict cancel changed working tree'
$script:resolve=$true
$r=Update-Mod $addon ([ref]$changed) ([ref]$pending)
check ($r -eq 'Updated.' -and $changed -and [IO.File]::ReadAllText("$conflictRoot/incoming").Trim() -eq 'shared conflict') 'Conflict choice not applied'
check (!(Test-Path "$conflictRoot/.git/MERGE_HEAD") -and !(Git $conflictRoot @('ls-files','--unmerged'))) 'Left unresolved merge'
'PASS: mod updates, staging preservation, selective backups, ignored files, conflict cancel and resolution'
