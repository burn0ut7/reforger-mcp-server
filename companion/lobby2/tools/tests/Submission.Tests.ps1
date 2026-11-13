$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../GC-ModWorkflow.ps1')
. (Join-Path $PSScriptRoot '../GC-SubmitMission.ps1')
function Assert($value,$message) {if(!$value){throw $message}}
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('gc-submission-test-'+[guid]::NewGuid())
$upstream=Join-Path $fixture 'shared';$author=Join-Path $fixture 'author';$forkPath=Join-Path $fixture 'personal.git'
$null=New-Item -ItemType Directory -Path $upstream -Force
$null=Git $upstream @('init','-q','-b','main')
$null=Git $upstream @('config','user.name','Fixture')
$null=Git $upstream @('config','user.email','fixture@example.invalid')
$worldPath='worlds/Example.ent';$headerPath='Missions/Example.conf'
$null=New-Item -ItemType Directory -Path "$upstream/worlds/Example_Layers","$upstream/Missions" -Force
[IO.File]::WriteAllText("$upstream/addon.gproj",('GameProject {'+[Environment]::NewLine+' GUID "62D55563549DA97A"'+[Environment]::NewLine+'}'))
[IO.File]::WriteAllText("$upstream/$worldPath",'base world')
[IO.File]::WriteAllText("$upstream/$worldPath.meta",'MetaFileClass {'+"`n"+' Name "{ABCD123456789012}worlds/Example.ent"'+"`n"+'}')
[IO.File]::WriteAllText("$upstream/$headerPath",'GC_MissionHeader {'+"`n"+' World "{ABCD123456789012}worlds/Example.ent"'+"`n"+' m_sName "COOP Alpha"'+"`n"+'}')
[IO.File]::WriteAllText("$upstream/$headerPath.meta",'header meta')
[IO.File]::WriteAllText("$upstream/worlds/Example_Layers/default.layer",'base layer')
[IO.File]::WriteAllText("$upstream/readme.txt",'shared readme')
$null=Git $upstream @('add','.')
$null=Git $upstream @('commit','-qm','Base')
$null=Git $upstream @('clone','-q',$upstream,$author)
$null=Git $upstream @('clone','-q','--bare',$upstream,$forkPath)
$null=Git $author @('config','user.name','Author')
$null=Git $author @('config','user.email','author@example.invalid')
$repo='Global-Conflicts-ArmA/gc-reforger-missions'
$null=Git $author @('config',"url.$upstream.insteadOf","https://github.com/$repo.git")
$null=Git $author @('config',"url.$forkPath.insteadOf",'https://tester@github.com/tester/gc-reforger-missions.git')
[IO.File]::WriteAllText("$author/readme.txt",'unrelated committed edit')
$null=Git $author @('add','readme.txt');$null=Git $author @('commit','-qm','Unrelated local commit')
[IO.File]::WriteAllText("$author/unrelated.txt",'private staged file');$null=Git $author @('add','unrelated.txt')
[IO.File]::WriteAllText("$author/$worldPath",'my first world')
[IO.File]::WriteAllText("$author/worlds/Example_Layers/default.layer",'my layer')
$head=Git $author @('rev-parse','HEAD');$indexHash=(Get-FileHash "$author/.git/index").Hash
$world=Join-Path $author $worldPath
$addons=@(@{Name='GC Missions';Key='GlobalConflictsMissions';Project="$author/addon.gproj";Guid='62D55563549DA97A';Repo=$repo})
$script:forkReady=$false;$script:created=0;$script:patched=0;$script:pr=$null;$script:cancel=$false;$script:choice='mine';$script:conflicts=0;$script:reviewMutation=$null
function Get-SubmissionAuth {@{}}
function Show-GcReview($spec) {
    if($spec.Title -eq 'Resolve conflicting file') {
        [IO.File]::WriteAllText((Join-Path $fixture 'conflict.json'),($spec|ConvertTo-Json -Depth 10))
        $script:conflicts++
        Assert ($spec.Fields[0].ReadOnly -and $spec.Fields[1].ReadOnly) 'Conflict previews editable'
        if($script:choice -eq 'cancel'){return}
        return @{Action=$script:choice;Values=@{}}
    }
    if($spec.Section -eq 'Submit for approval') {
        if(!$script:pr){[IO.File]::WriteAllText((Join-Path $fixture 'submission.json'),($spec|ConvertTo-Json -Depth 10))}
        if($script:cancel){return}
        if($script:reviewMutation){& $script:reviewMutation;$script:reviewMutation=$null}
        Assert (@($spec.Fields | Where-Object Id -eq excluded).Count -eq 1) 'Excluded files not shown'
        return @{Action='confirm';Values=@{summary='Escort the convoy';notes='Review objectives';dependencies='';body='# Overview'+"`n"+'Updated mission';scope=$true}}
    }
    if($spec.Title -eq 'Mission submitted'){return @{Action='confirm';Values=@{}}}
    throw "Unexpected dialog: $($spec.Title)"
}
function Invoke-SubmissionApi($auth,$method,$path,$data,[switch]$AllowMissing) {
    if($path -eq 'user'){return @{login='tester';id=42}}
    if($path -eq 'repos/tester/gc-reforger-missions') {if($script:forkReady){return @{fork=$true;parent=@{full_name=$repo};default_branch='main'}};return}
    if($method -eq 'POST' -and $path -eq "repos/$repo/forks") {$script:forkReady=$true;return @{full_name='tester/gc-reforger-missions'}}
    if($method -eq 'GET' -and $path.Contains('/pulls?')){return $script:pr}
    if($method -eq 'POST' -and $path.EndsWith('/pulls')) {
        $script:created++
        $script:branch=$data.head.Substring('tester:'.Length)
        Assert ($data.base -eq 'main' -and $data.maintainer_can_modify) 'Wrong PR destination'
        $script:pr=@{number=12;body=$data.body;title=$data.title;mergeable=$true;head=@{repo=@{owner=@{login='tester'}};ref=$script:branch};base=@{ref='main'}}
        return $script:pr
    }
    if($path.EndsWith('/pulls/12')) {
        if($method -eq 'PATCH'){$script:patched++;$script:pr.body=$data.body;$script:pr.title=$data.title}
        return $script:pr
    }
    throw "Unexpected API: $method $path"
}
function Check-Isolation {
    Assert ((Git $author @('rev-parse','HEAD')) -eq $head) 'Working HEAD changed'
    Assert ((Git $author @('branch','--show-current')) -eq 'main') 'Working branch changed'
    Assert ((Get-FileHash "$author/.git/index").Hash -eq $indexHash) 'Real index changed'
    Assert ((Git $forkPath @('show',($script:branch+':readme.txt'))) -eq 'shared readme') 'Unrelated committed data leaked'
    Assert (!(Git $forkPath @('ls-tree',$script:branch,'--','unrelated.txt'))) 'Private staging leaked'
}
Assert ((Invoke-MissionSubmission $addons $world) -eq 0) 'Initial submission failed'
Assert ($created -eq 1 -and $script:branch -eq 'mission/coop-alpha-abcd123456789012') 'Automatic branch or PR wrong'
Check-Isolation
[IO.File]::WriteAllText($world,'my second world')
Assert ((Invoke-MissionSubmission $addons $world) -eq 0 -and $created -eq 1 -and $patched -eq 1) 'Existing PR not reused'
Check-Isolation
$remoteBefore=Git $forkPath @('rev-parse',$script:branch)
$script:cancel=$true
Assert ((Invoke-MissionSubmission $addons $world) -eq 2) 'Cancel result wrong'
Assert ((Git $forkPath @('rev-parse',$script:branch)) -eq $remoteBefore) 'Cancel uploaded changes'
$script:cancel=$false
[IO.File]::WriteAllText("$upstream/$worldPath",'shared edit')
$null=Git $upstream @('add','.');$null=Git $upstream @('commit','-qm','Shared update')
[IO.File]::WriteAllText($world,'my third world')
$script:choice='cancel'
Assert ((Invoke-MissionSubmission $addons $world) -eq 2) 'Conflict cancel result wrong'
Assert ((Git $forkPath @('rev-parse',$script:branch)) -eq $remoteBefore) 'Conflict cancel uploaded'
$script:choice='mine'
Assert ((Invoke-MissionSubmission $addons $world) -eq 0 -and $conflicts -ge 2) 'Conflict choice did not run'
Assert ((Git $forkPath @('show',($script:branch+':'+$worldPath))) -eq 'my third world') 'My version not retained'
Check-Isolation
[IO.File]::WriteAllText("$upstream/$worldPath",'shared edit two');$null=Git $upstream @('add','.');$null=Git $upstream @('commit','-qm','Shared update two')
[IO.File]::WriteAllText($world,'my fourth world');$script:choice='shared'
Assert ((Invoke-MissionSubmission $addons $world) -eq 0) 'Shared conflict choice failed'
Assert ((Git $forkPath @('show',($script:branch+':'+$worldPath))) -eq 'shared edit two') 'Shared version not submitted'
Assert ([IO.File]::ReadAllText($world) -eq 'my fourth world') 'Conflict choice changed author files'
Assert ((Invoke-MissionSubmission $addons $world) -eq 0) 'Repeat submission failed'
Assert ((Git $forkPath @('show',($script:branch+':'+$worldPath))) -eq 'shared edit two') 'Repeat lost previous resolution'
Check-Isolation
$remoteBefore=Git $forkPath @('rev-parse',$script:branch)
$script:reviewMutation={ [IO.File]::WriteAllText($world,'edited during review') }
try {$null=Invoke-MissionSubmission $addons $world;throw 'Stale review accepted'} catch {Assert ($_.Exception.Message -eq 'Your mission changed during review. Submit again.') $_.Exception.Message}
Assert ((Git $forkPath @('rev-parse',$script:branch)) -eq $remoteBefore) 'Stale review uploaded'
[IO.File]::WriteAllText("$forkPath/hooks/pre-receive", "#!/bin/sh`nexit 1`n", (New-Object Text.UTF8Encoding($false)))
try {$null=Invoke-MissionSubmission $addons $world;throw 'Rejected push accepted'} catch {Assert ($_.Exception.Message -like 'Upload stopped.*') $_.Exception.Message}
[IO.File]::Delete("$forkPath/hooks/pre-receive")
Assert ((Invoke-MissionSubmission $addons $world) -eq 0) 'Retry after rejected push failed'
Check-Isolation
$remoteBefore=Git $forkPath @('rev-parse',$script:branch)
$remoteTree=Git $forkPath @('rev-parse',($script:branch+'^{tree}'))
$outside=Git $forkPath @('-c','user.name=Other author','-c','user.email=other@example.invalid','commit-tree',$remoteTree,'-p',$remoteBefore,'-m','External edit')
$null=Git $forkPath @('update-ref',('refs/heads/'+$script:branch),$outside)
try {$null=Invoke-MissionSubmission $addons $world;throw 'External submission edit accepted'} catch {Assert ($_.Exception.Message -like 'This submission changed*' -or $_.Exception.Message -like 'Git failed*') $_.Exception.Message}
Assert ((Git $forkPath @('rev-parse',$script:branch)) -eq $outside) 'External submission overwritten'
Check-Isolation
Write-Output "PASS: automatic fork/mission branch, scoped submission, unchanged checkout/index, existing PR update, cancellation, both conflict choices, retained resolutions, stale review, rejected push retry. Fixture: $fixture"
