# Loaded by GC-ModWorkflow.ps1 for the Submit Mission action.
function Get-SubmissionMission($root, $world) {
    $prefix = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    $world = (Get-Item -LiteralPath $world).FullName
    if (!$world.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($world) -ne '.ent') {
        throw 'Open a saved world in GC Missions.'
    }
    $relative = $world.Substring($prefix.Length).Replace('\','/')
    $meta = [IO.File]::ReadAllText("$world.meta")
    if ($meta -notmatch '(?m)^\s*Name\s+"(\{[0-9A-Fa-f]{16}\}[^"]+)"') { throw 'Save and register the mission world first.' }
    $resource = $Matches[1]
    $headers = @(foreach ($folder in 'Missions','Configs/Missions') {
        $dir = Join-Path $root $folder
        if (!(Test-Path -LiteralPath $dir -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $dir -Filter '*.conf' -File -Recurse) {
            $data = [IO.File]::ReadAllText($file.FullName)
            if ($data -notmatch '(?m)^\s*World\s+"([^"]+)"' -or $Matches[1] -ine $resource) { continue }
            if ($data -notmatch '(?m)^\s*m_sName\s+"([^"\r\n]+)"\s*$') { throw 'Enter a mission name in the mission header.' }
            $title = $Matches[1].Trim()
            if (!$title) { throw 'Enter a mission name in the mission header.' }
            if (!(Test-Path -LiteralPath ($file.FullName+'.meta'))) { throw 'Register the mission header in Workbench first.' }
            @{ File=$file.FullName.Substring($prefix.Length).Replace('\','/'); Title=$title }
        }
    })
    if (!$headers.Count) { throw 'No mission header found. Run GC > Create Mission Data first.' }
    if ($headers.Count -ne 1) { throw 'Two headers point to this world. Choose one before submitting.' }
    $header = $headers[0]
    $layers = $relative.Substring(0,$relative.Length-4) + '_Layers'
    $required = @($relative, "$relative.meta", $header.File, ($header.File+'.meta'))
    @{Title=$header.Title; Id=$resource.Substring(1,16).ToLowerInvariant(); Required=$required; Scope=($required + @($layers))}
}

function Get-OpenMissionPr($auth,$repo,$owner,$branch,$base) {
    $source=[Uri]::EscapeDataString("${owner}:$branch")
    $baseName=[Uri]::EscapeDataString($base)
    $items=Invoke-SubmissionApi $auth GET "repos/$repo/pulls?state=open&head=$source&base=$baseName"
    $items | Where-Object { $_.head.repo.owner.login -eq $owner -and $_.head.ref -ceq $branch -and $_.base.ref -ceq $base } | Select-Object -First 1
}

function Get-SubmissionAuth {
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $credential = "protocol=https`nhost=github.com`n`n" | git.exe credential fill 2>$null
        if ($LASTEXITCODE -ne 0) { throw 'Sign in to GitHub and try again.' }
        $password = $credential | Where-Object { $_ -clike 'password=*' } | Select-Object -First 1
        if (!$password) { throw 'Sign in to GitHub and try again.' }
        @{Authorization='Bearer '+$password.Substring(9); Accept='application/vnd.github+json'; 'User-Agent'='GC-Workbench'}
    } finally { $ErrorActionPreference = $previous }
}

function Invoke-SubmissionApi($auth, $method, $path, $data, [switch]$AllowMissing) {
    $request = @{Uri="https://api.github.com/$path"; Headers=$auth; Method=$method; ErrorAction='Stop'; TimeoutSec=30}
    if ($null -ne $data) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($data | ConvertTo-Json -Compress))
    }
    try { Invoke-RestMethod @request } catch {
        $status = 0
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($AllowMissing -and $status -eq 404) { return }
        if ($status -in 401,403) { throw 'GitHub denied access. Check your sign-in and repository permissions.' }
        throw "GitHub could not complete the request ($status). Retry Submit Mission."
    }

}

function New-SubmissionCommit($root, $tree, $parents, $title, $user) {
    $arguments=@('-c',('user.name='+$user.login),'-c',('user.email='+$user.id+'+'+$user.login+'@users.noreply.github.com'),'-c','commit.gpgSign=false','commit-tree',$tree)
    foreach ($parent in @($parents | Select-Object -Unique)) { $arguments+=@('-p',$parent) }
    Git $root ($arguments+@('-m',$title))
}

function Get-SubmissionRef($root,$ref) {
    $previous=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        $value=@(& git.exe -C $root rev-parse --verify --quiet $ref 2>$null)
        if ($LASTEXITCODE -eq 0) { return [string]$value[0] }
        if ($LASTEXITCODE -ne 1) { throw 'Could not read the saved submission.' }
    } finally { $ErrorActionPreference=$previous }
}

function Test-MissionPath($mission,$path) {
    foreach ($scope in $mission.Scope) {
        if ($path -ceq $scope -or $path.StartsWith($scope+'/',[StringComparison]::Ordinal)) { return $true }
    }
    return $false
}

function Get-MissionSnapshot($root,$mission,$base,$tip) {
    $index=Join-Path ([IO.Path]::GetTempPath()) ('gc-submit-'+[guid]::NewGuid()+'.index')
    $previous=$env:GIT_INDEX_FILE
    try {
        $local=@(Git $root @('-c','core.quotePath=false','diff','--name-only','--no-renames',$base))
        $local+=@(Git $root @('-c','core.quotePath=false','ls-files','--others','--exclude-standard'))
        $env:GIT_INDEX_FILE=$index
        $null=Git $root @('read-tree',$base)
        foreach ($scope in $mission.Scope) {
            $tracked=@(Git $root @('--literal-pathspecs','ls-files','--cached','--',$scope))
            if ((Test-Path -LiteralPath (Join-Path $root $scope)) -or $tracked.Count) {
                $null=Git $root @('--literal-pathspecs','add','-A','--',$scope)
            }
        }
        $tree=Git $root @('write-tree')
        Assert-MissionTree $root $mission $base $tree
        $files=@(Git $root @('-c','core.quotePath=false','diff','--name-only','--no-renames',$base,$tree))
        $excluded=@($local | Where-Object { !(Test-MissionPath $mission $_) } | Sort-Object -Unique)
        @{Tree=$tree;Files=$files;Excluded=$excluded}
    } finally {
        $env:GIT_INDEX_FILE=$previous
        if ([IO.File]::Exists($index)) { [IO.File]::Delete($index) }
    }
}

function Assert-MissionTree($root,$mission,$base,$tree) {
    foreach ($path in @(Git $root @('-c','core.quotePath=false','diff','--name-only','--no-renames',$base,$tree))) {
        if (!(Test-MissionPath $mission $path)) { throw "Submission includes another mission's file: $path. Nothing was uploaded." }
    }
    foreach ($path in $mission.Required) {
        $entry=@(Git $root @('--literal-pathspecs','ls-tree',$tree,'--',$path))
        if ($entry.Count -ne 1 -or $entry[0] -notmatch '^100(644|755) blob ') { throw "The mission still needs this file: $path. Nothing was uploaded." }
    }
    $metadata=(Git $root @('show',($tree+':'+$mission.Required[1]))) -join "`n"
    $header=(Git $root @('show',($tree+':'+$mission.Required[2]))) -join "`n"
    if ($metadata -notmatch '(?m)^\s*Name\s+"(\{[0-9A-Fa-f]{16}\}[^"]+)"') { throw 'Mission registration is incomplete. Save your mission and try again.' }
    $resource=$Matches[1]
    if ($header -notmatch '(?m)^\s*World\s+"([^"]+)"' -or $Matches[1] -ine $resource) { throw 'These mission files do not match. Choose matching versions of the world and mission data.' }
}

function Read-MissionSubmission($mission,$existing,$files,$excluded) {
    $fields=@(@{Id='summary';Label='What changed?';Kind='multiline';Required=$true},
        @{Id='notes';Label='Reviewer notes';Kind='multiline'},@{Id='dependencies';Label='Other required mods';Kind='text'})
    if ($existing) {
        # Preserve all existing reviewer-facing content unless the author edits it.
        $fields=@(@{Id='body';Label='Submission details';Kind='multiline';Required=$true;Value=[string]$existing.body})
    } else {
        foreach ($test in @(@('briefing','Briefing, map and slots'),@('timers','Timers and area limits'),@('prefabs','Custom prefabs'),@('events','Mission events'),@('ending','End conditions'),@('server','Dedicated server / peer test'))) {
            $fields+=@{Id=$test[0];Label=$test[1];Kind='check'}
        }
    }
    $fields+=@{Id='files';Label='Files to submit';Kind='multiline';ReadOnly=$true;Value=($files -join "`n")}
    if ($excluded.Count) {
        $fields+=@{Id='excluded';Label='Not included';Kind='multiline';ReadOnly=$true;Value=($excluded -join "`n")}
        $fields+=@{Id='scope';Label='My mission does not need the excluded files';Kind='check';Required=$true}
    }
    $answer=Show-GcReview @{Title=$mission.Title;Section='Submit for approval';PrimaryAction=$(if($existing){'Update submission'}else{'Submit mission'});Fields=$fields}
    if (!$answer) { return }
    if ($existing) { return $answer.Values.body }
    $v=$answer.Values
    $testing=@()
    foreach ($test in @(@('briefing','Briefing, map, slotting screen'),@('timers','Setup timers / AO Limits'),@('prefabs','Custom prefabs'),@('events','Key framework events and interactions'),@('ending','End conditions'),@('server','Playable in Dedicated Server Tool / Peer Tool'))) {
        $mark=if($v.($test[0])){'x'}else{' '}
        $testing+="- [$mark] $($test[1])"
    }
    "# Overview`n$($v.summary)`n`n## Testing`n$($testing -join "`n")`n`n## Comments for the reviewer`n$($v.notes)`n`n## Dependencies`n$($v.dependencies)"
}

function Invoke-MissionSubmission($addons,$world) {
    $missions=$addons | Where-Object Key -eq GlobalConflictsMissions
    $root=Get-Checkout $missions.Project $missions.Guid
    $mission=Get-SubmissionMission $root $world
    $mutex=New-Object Threading.Mutex($false,('Local\GCSubmission-'+$mission.Id))
    $locked=$false
    try {
        try {$locked=$mutex.WaitOne(0)} catch [Threading.AbandonedMutexException] {$locked=$true}
        if (!$locked) { throw 'This mission already has a submission window open.' }
        Invoke-MissionSubmissionCore $root $missions.Repo $mission $world
    } finally {
        if ($locked) {$mutex.ReleaseMutex()}
        $mutex.Dispose()
    }
}

function Invoke-MissionSubmissionCore($root,$repo,$mission,$world) {
    $auth=Get-SubmissionAuth
    $user=Invoke-SubmissionApi $auth GET 'user'
    $owner=$user.login
    if ($owner -notmatch '^[A-Za-z0-9-]+$' -or [string]$user.id -notmatch '^\d+$') { throw 'Sign in to GitHub first.' }
    $base=Get-ModBase $root $repo
    $null=Git $root @('fetch','--quiet','--no-tags',"https://github.com/$repo.git",('refs/heads/'+$base.Name))
    $tip=Git $root @('rev-parse','HEAD')
    $ancestor=Git $root @('merge-base',$tip,$base.Commit)
    $snapshot=Get-MissionSnapshot $root $mission $ancestor $tip
    $prefix="refs/gc/submissions/$owner/$($mission.Id)"
    $previousTip=Get-SubmissionRef $root "$prefix/tip"
    $previousSource=Get-SubmissionRef $root "$prefix/source"
    if ([bool]$previousTip -ne [bool]$previousSource) { throw 'Saved submission needs a mission reviewer. Your mission files are safe.' }
    $branchKey="gcmission.$($mission.Id).branch"
    $previousPreference=$ErrorActionPreference
    try {$ErrorActionPreference='Continue';$branch=& git.exe -C $root config --local --get $branchKey 2>$null} finally {$ErrorActionPreference=$previousPreference}
    if (!$branch) {
        $slug=($mission.Title.ToLowerInvariant() -replace '[^a-z0-9]+','-').Trim('-')
        if (!$slug) {$slug='mission'}
        if ($slug.Length -gt 50) {$slug=$slug.Substring(0,50).TrimEnd('-')}
        $branch="mission/$slug-$($mission.Id)"
    }
    $null=Git $root @('check-ref-format','--branch',$branch)
    $name=($repo -split '/')[1];$forkRepo="$owner/$name"
    $fork=Invoke-SubmissionApi $auth GET "repos/$forkRepo" $null -AllowMissing
    if ($fork -and (!$fork.fork -or $fork.parent.full_name -ine $repo)) { throw 'A repository with this name already exists on your account. Ask a mission reviewer to help connect your fork.' }
    if ($fork -and $fork.default_branch -ceq $branch) { throw 'This submission name is reserved. Ask a mission reviewer for help.' }
    $url="https://$owner@github.com/$forkRepo.git"
    $remoteTip=''
    if ($fork) {
        $remote=@(Git $root @('ls-remote','--heads',$url,"refs/heads/$branch"))
        if ($remote.Count) {$remoteTip=($remote[0] -split '\s+')[0]}
        if ($remoteTip -and (!$previousTip -or !(Get-SubmissionRef $root "$remoteTip^{commit}") -or (Git $root @('merge-base',$remoteTip,$previousTip)) -ne $remoteTip)) { throw 'This submission changed on another computer. Nothing was overwritten. Ask a mission reviewer to combine the versions.' }
    }
    $existing=Get-OpenMissionPr $auth $repo $owner $branch $base.Name
    $source=New-SubmissionCommit $root $snapshot.Tree @($ancestor) $mission.Title $user
    $candidate=$source
    if ($previousTip) {
        $tree=Merge-GcReviewedTree $root $source $previousTip $mission.Title $previousSource
        if (!$tree) {return 2}
        $candidate=New-SubmissionCommit $root $tree @($previousTip,$source) $mission.Title $user
    }
    $tree=Merge-GcReviewedTree $root $candidate $base.Commit $mission.Title
    if (!$tree) {return 2}
    Assert-MissionTree $root $mission $base.Commit $tree
    $files=@(Git $root @('-c','core.quotePath=false','diff','--name-only','--no-renames',$base.Commit,$tree))
    if (!$files.Count) { throw 'This mission already matches the shared version. Nothing to submit.' }
    $body=Read-MissionSubmission $mission $existing $files $snapshot.Excluded
    if (!$body) {return 2}
    # Refuse stale reviews. No checkout, index, or working files have been changed.
    $latest=Get-ModBase $root $repo
    if ($latest.Commit -ne $base.Commit -or $latest.Name -cne $base.Name) { throw 'The shared missions changed during review. Submit again to compare the latest version.' }
    if ((Git $root @('rev-parse','HEAD')) -ne $tip) { throw 'Your mission changed during review. Submit again.' }
    $freshMission=Get-SubmissionMission $root $world
    $fresh=Get-MissionSnapshot $root $freshMission $ancestor $tip
    if ($fresh.Tree -ne $snapshot.Tree -or $freshMission.Title -cne $mission.Title) { throw 'Your mission changed during review. Submit again.' }
    $currentPr=Get-OpenMissionPr $auth $repo $owner $branch $base.Name
    if ($currentPr.number -ne $existing.number -or $currentPr.body -cne $existing.body -or $currentPr.title -cne $existing.title) { throw 'Your submission changed during review. Submit again.' }
    if (!$fork) {
        $created=Invoke-SubmissionApi $auth POST "repos/$repo/forks" @{name=$name;default_branch_only=$true}
        if ($created.full_name -ine $forkRepo) { throw 'GitHub is preparing your account. Retry Submit Mission shortly.' }
        # Fork creation is asynchronous. Read-only readiness polling is bounded.
        $ready=$false
        for ($attempt=0;$attempt -lt 10;$attempt++) {
            $check=Invoke-SubmissionApi $auth GET "repos/$forkRepo" $null -AllowMissing
            if ($check -and $check.fork -and $check.parent.full_name -ieq $repo) {$ready=$true;break}
            Start-Sleep -Milliseconds 1000
        }
        if (!$ready) { throw 'GitHub is still preparing your account. Retry Submit Mission shortly.' }
    }
    $parents=@($base.Commit)
    if ($previousTip) {$parents+=@($previousTip)}
    $commit=New-SubmissionCommit $root $tree $parents $mission.Title $user
    $null=Git $root @('config','--local',$branchKey,$branch)
    # Preserve both reviewed content and source before network writes, including a
    # push that succeeds remotely but loses its response. Retry never force-pushes.
    $null=Git $root @('update-ref',"$prefix/tip",$commit)
    $null=Git $root @('update-ref',"$prefix/source",$source)
    try {$null=Git $root @('push','--quiet','--no-follow-tags','--recurse-submodules=no',$url,"${commit}:refs/heads/$branch")}
    catch {throw 'Upload stopped. Your mission and prepared submission are safe. Retry Submit Mission; nothing will be force-overwritten.'}
    $fields=@{title=$mission.Title;body=$body}
    if ($existing) {$pr=Invoke-SubmissionApi $auth PATCH "repos/$repo/pulls/$($existing.number)" $fields}
    else {
        $fields.head="${owner}:$branch";$fields.base=$base.Name;$fields.maintainer_can_modify=$true
        $pr=Invoke-SubmissionApi $auth POST "repos/$repo/pulls" $fields
    }
    if (!$pr.number) {throw 'Upload saved. Approval request could not be confirmed. Retry Submit Mission.'}
    # GitHub may still be calculating mergeability; never call an unknown result clean.
    $status=Invoke-SubmissionApi $auth GET "repos/$repo/pulls/$($pr.number)"
    $state=if ($status.mergeable -eq $false) {'Needs conflict review'} elseif ($status.mergeable -eq $true) {'Awaiting approval'} else {'Awaiting approval - conflict check pending'}
    $result=Show-GcReview @{Title='Mission submitted';PrimaryAction='Done';SecondaryAction='';Items=@(@{Title=$mission.Title;Status=$state;State=$(if($status.mergeable -eq $false){'warning'}else{'ready'})});Fields=@(@{Id='url';Label='Approval request';Value="https://github.com/$repo/pull/$($pr.number)";ReadOnly=$true})}
    return 0
}
