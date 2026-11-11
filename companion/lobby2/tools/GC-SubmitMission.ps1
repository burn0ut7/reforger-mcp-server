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
    @{Title=$header.Title; Required=$required; Scope=($required + @($layers))}
}

# Prepare exactly the saved mission files in a temporary index. Unrelated staged
# files stay in the author's index. The resulting tree pins the reviewed content.
function Get-MissionPreview($root, $mission, $base, $tip) {
    $local = @(Git $root @('-c','core.quotePath=false','diff','--name-only','--no-renames',$tip))
    $local += @(Git $root @('-c','core.quotePath=false','ls-files','--others','--exclude-standard'))
    $index = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '.index')
    $previous = $env:GIT_INDEX_FILE
    try {
        $env:GIT_INDEX_FILE = $index
        $null = Git $root @('read-tree',$tip)
        $files = @(Git $root (@('-c','core.quotePath=false','--literal-pathspecs','ls-files','--cached','--others','--exclude-standard','--') + $mission.Scope) | Sort-Object -Unique)
        if ($files | Where-Object { $_.StartsWith('"') }) { throw 'A mission filename cannot be read. Rename it before submitting.' }
        foreach ($path in $mission.Required) {
            if ($files -cnotcontains $path) { throw "Missing mission file: $path" }
        }
        # Use the small set of directory/file pathspecs, including deleted tracked layers.
        foreach ($path in $mission.Scope) {
            if ($files -ccontains $path -or @($files | Where-Object { $_.StartsWith($path+'/',[StringComparison]::Ordinal) }).Count) {
                $null = Git $root @('--literal-pathspecs','add','-A','--',$path)
            }
        }
        $tree = Git $root @('write-tree')
        $registered = @(Git $root (@('-c','core.quotePath=false','--literal-pathspecs','ls-tree','-r','--name-only',$tree,'--') + $mission.Required))
        foreach ($path in $mission.Required) {
            if ($registered -cnotcontains $path) { throw "Missing mission file: $path" }
        }
        $delta = @(Git $root @('-c','core.quotePath=false','diff','--no-renames','--name-only',$base,$tree))
        if (!@($delta | Where-Object { $files -ccontains $_ }).Count) { throw 'This mission has no changes to submit.' }
        $pending = @(Git $root @('-c','core.quotePath=false','diff','--no-renames','--name-only',$tip,$tree))
        $excluded = @($local | Where-Object { $files -cnotcontains $_ } | Sort-Object -Unique)
        @{Tree=$tree; Files=$delta; Pending=$pending; Excluded=$excluded}
    } finally {
        $env:GIT_INDEX_FILE = $previous
        if ([IO.File]::Exists($index)) { [IO.File]::Delete($index) }
    }
}

function Read-MissionSubmission($title,$destination,$template,$existing,$files,$excluded) {
    $pattern='(?s)\A(?<heading>#[^\r\n]+)\r?\n(?<overview>.*?)(?<details>\r?\n##\s.*)\z'
    $layout=[regex]::Match($template,$pattern)
    if (!$layout.Success) { throw 'The GC pull request template changed. Check its format before submitting.' }
    $source=$layout
    if ($existing) {
        $source=[regex]::Match([string]$existing.body,$pattern)
        if (!$source.Success) { throw 'Edit this existing pull request on GitHub.' }
    }
    $form=New-Object Windows.Forms.Form
    try {
        $form.Text='Submit Mission'; $form.ClientSize=New-Object Drawing.Size(720,600)
        $form.MinimumSize=New-Object Drawing.Size(540,450); $form.StartPosition='CenterScreen'
        $grid=New-Object Windows.Forms.TableLayoutPanel
        $grid.Dock='Fill'; $grid.Padding=10; $grid.ColumnCount=1; $grid.RowCount=8
        $form.Controls.Add($grid)
        foreach ($size in 48,23,70,23,-65,23,-35,42) {
            $style=New-Object Windows.Forms.RowStyle
            if ($size -lt 0) { $style.SizeType='Percent'; $style.Height=-$size } else { $style.SizeType='Absolute'; $style.Height=$size }
            $null=$grid.RowStyles.Add($style)
        }
        function Add-Label($value,$row) {
            $label=New-Object Windows.Forms.Label
            $label.Text=$value; $label.Dock='Fill'; $grid.Controls.Add($label,0,$row)
        }
        function Add-Box($value,$row) {
            $box=New-Object Windows.Forms.TextBox
            $box.Dock='Fill'; $box.Multiline=$true; $box.ScrollBars='Vertical'; $box.AcceptsReturn=$true
            $box.Text=$value -replace '\r?\n',"`r`n"
            $grid.Controls.Add($box,0,$row)
            return $box
        }
        Add-Label "$title`r`n$destination" 0
        Add-Label 'Describe your mission or what changed (required)' 1
        $overview=''
        if ($existing) { $overview=$source.Groups['overview'].Value.Trim() }
        $summary=Add-Box $overview 2
        Add-Label 'Testing, reviewer notes and dependencies' 3
        $details=Add-Box $source.Groups['details'].Value.TrimStart() 4
        $fileLabel = 'Files in this pull request'
        $display = @($files)
        if ($excluded.Count) {
            $fileLabel = "Files to submit ($($excluded.Count) local files excluded; see below)"
            $display += @('','NOT INCLUDED - commit these first if your mission needs them:') + $excluded
        }
        Add-Label $fileLabel 5
        $list=Add-Box ($display -join "`r`n") 6
        $list.ReadOnly=$true
        $buttons=New-Object Windows.Forms.FlowLayoutPanel
        $buttons.Dock='Fill'; $buttons.FlowDirection='RightToLeft'
        $submit=New-Object Windows.Forms.Button
        $submit.Text=if ($existing) {'Update pull request'} else {'Submit mission'}
        $submit.AutoSize=$true
        $cancel=New-Object Windows.Forms.Button
        $cancel.Text='Cancel'; $cancel.DialogResult='Cancel'
        $submit.Add_Click({
            if (!$summary.Text.Trim() -or $summary.Text.Trim() -eq $layout.Groups['overview'].Value.Trim()) {
                $null=Show-Message 'Describe your mission first.'
                $null=$summary.Focus()
                return
            }
            foreach ($heading in [regex]::Matches($layout.Groups['details'].Value,'(?m)^## [^\r\n]+')) {
                if ($details.Lines -cnotcontains $heading.Value) { $null=Show-Message "Keep this heading: $($heading.Value)"; return }
            }
            $form.DialogResult='OK'
        })
        $buttons.Controls.AddRange(@($submit,$cancel)); $grid.Controls.Add($buttons,0,7)
        $form.CancelButton=$cancel; $form.ActiveControl=$summary
        if ($form.ShowDialog() -eq 'OK') {
            return $layout.Groups['heading'].Value + "`n" + $summary.Text.Trim() + "`n`n" + $details.Text.Trim()
        }
    } finally { $form.Dispose() }
}

function Save-SubmissionCommit($root,$preview,$title,$tip) {
    if (!$preview.Pending.Count) { return $tip }
    $spec=Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '.paths')
    try {
        $staged=@(Git $root @('-c','core.quotePath=false','diff','--cached','--name-only','--no-renames'))
        $unstaged=@(Git $root @('-c','core.quotePath=false','diff','--name-only','--no-renames'))
        foreach ($path in $preview.Pending) {
            if ($staged -ccontains $path -and $unstaged -ccontains $path) { throw "Commit your staged edits to $path first." }
        }
        [IO.File]::WriteAllText($spec,($preview.Pending -join [char]0) + [char]0,(New-Object Text.UTF8Encoding($false)))
        $selection=@("--pathspec-from-file=$spec",'--pathspec-file-nul')
        $null=Git $root (@('--literal-pathspecs','add','-A') + $selection)
        $null=Git $root (@('--literal-pathspecs','commit','--quiet','--only','-m',$title) + $selection)
        $commit=Git $root @('rev-parse','HEAD')
        if ((Git $root @('rev-parse',"$commit^")) -ne $tip -or (Git $root @('rev-parse',"$commit^{tree}")) -ne $preview.Tree) {
            throw 'The commit changed during review. Check it before submitting again.'
        }
        return $commit
    } finally { if ([IO.File]::Exists($spec)) { [IO.File]::Delete($spec) } }
}


function Get-OpenMissionPr($auth,$repo,$owner,$branch,$base) {
    $source=[Uri]::EscapeDataString("${owner}:$branch")
    $baseName=[Uri]::EscapeDataString($base)
    $items=Invoke-SubmissionApi $auth GET "repos/$repo/pulls?state=open&head=$source&base=$baseName"
    $items | Where-Object { $_.head.repo.owner.login -eq $owner -and $_.head.ref -ceq $branch -and $_.base.ref -ceq $base } | Select-Object -First 1
}

function Invoke-MissionSubmission($addons,$world) {
    $missions=$addons | Where-Object Key -eq GlobalConflictsMissions
    $repo=$missions.Repo
    $root=Get-Checkout $missions.Project $missions.Guid
    $mission=Get-SubmissionMission $root $world
    $base=Get-ModBase $root $repo
    $branch=Git $root @('symbolic-ref','--short','HEAD')
    if ($branch -ceq $base.Name) {
        if (!(New-MissionBranch $root $branch $world)) { return 2 }
        $branch=Git $root @('symbolic-ref','--short','HEAD')
        if ($branch -ceq $base.Name) { throw 'Choose a mission branch first.' }
    }
    $changed=$false; $desktop=$false
    foreach ($addon in $addons) {
        $result=Update-Mod $addon ([ref]$changed) ([ref]$desktop) -ForSubmission
        if ($desktop -or $result -notin 'Up to date.','Updated.') {
            throw "$($addon.Name): $result Run Update Mods before submitting."
        }
    }
    if ($changed) { throw 'Mods updated. Reload Workbench, then submit again.' }
    $tip=Git $root @('rev-parse','HEAD')
    $preview=Get-MissionPreview $root $mission $base.Commit $tip
    $template=(Git $root @('show',($base.Commit + ':.github/pull_request_template.md'))) -join "`n"
    $auth=Get-SubmissionAuth
    $owner=(Invoke-SubmissionApi $auth GET 'user').login
    if (!$owner -or $owner -notmatch '^[A-Za-z0-9-]+$') { throw 'Sign in to GitHub first.' }
    $name=($repo -split '/')[1]
    $forkRepo="$owner/$name"
    $fork=Invoke-SubmissionApi $auth GET "repos/$forkRepo" $null -AllowMissing
    if ($fork -and (!$fork.fork -or $fork.parent.full_name -ine $repo)) { throw 'Your GitHub repository is not a fork of GC Missions.' }
    if ($fork -and $fork.default_branch -ceq $branch) { throw 'Choose a separate mission branch first.' }
    $existing=Get-OpenMissionPr $auth $repo $owner $branch $base.Name
    $destination="$forkRepo ($branch)  ->  $repo ($($base.Name))"
    if ($existing) { $destination+="  (PR #$($existing.number))" }
    if (!$fork) { $destination+='  (creates your fork)' }
    $body=Read-MissionSubmission $mission.Title $destination $template $existing $preview.Files $preview.Excluded
    if (!$body) { return 2 }

    # Recheck all four upstreams after the review window closes.
    foreach ($addon in $addons) {
        $checkout=Get-Checkout $addon.Project $addon.Guid
        $latest=Get-ModBase $checkout $addon.Repo
        if ((Git $checkout @('merge-base','HEAD',$latest.Commit)) -ne $latest.Commit) { throw "$($addon.Name) changed. Run Update Mods and retry." }
        if (Git $checkout @('ls-files','--unmerged')) { throw "Resolve the conflict in $($addon.Name) first." }
        if ($addon.Key -eq $missions.Key -and ($latest.Commit -ne $base.Commit -or $latest.Name -cne $base.Name)) { throw 'GC Missions changed. Review again.' }
    }
    if ((Git $root @('symbolic-ref','--short','HEAD')) -cne $branch -or (Git $root @('rev-parse','HEAD')) -ne $tip) { throw 'Your branch changed. Review again.' }
    $currentMission=Get-SubmissionMission $root $world
    if ($currentMission.Title -cne $mission.Title -or ($currentMission.Required -join "`n") -cne ($mission.Required -join "`n")) { throw 'Mission data changed. Review again.' }
    $fresh=Get-MissionPreview $root $currentMission $base.Commit $tip
    if ($fresh.Tree -ne $preview.Tree) { throw 'Your mission changed. Review again.' }
    $currentPr=Get-OpenMissionPr $auth $repo $owner $branch $base.Name
    if ($currentPr.number -ne $existing.number -or $currentPr.body -cne $existing.body -or $currentPr.title -cne $existing.title) {
        throw 'The pull request changed on GitHub. Review it again.'
    }
    $commit=Save-SubmissionCommit $root $preview $mission.Title $tip
    if (!$fork) {
        $created=Invoke-SubmissionApi $auth POST "repos/$repo/forks" @{name=$name}
        if ($created.full_name -ine $forkRepo) { throw 'Check your fork on GitHub, then retry.' }
    }
    $url="https://$owner@github.com/$forkRepo.git"
    try { $null=Git $root @('push','--quiet','--no-follow-tags','--recurse-submodules=no',$url,"${commit}:refs/heads/$branch") }
    catch { throw 'Your local commit is safe. Check your fork on GitHub, then retry Submit Mission.' }
    $fields=@{title=$mission.Title; body=$body}
    if ($existing) {
        $pr=Invoke-SubmissionApi $auth PATCH "repos/$repo/pulls/$($existing.number)" $fields
    } else {
        $fields.head="${owner}:$branch"; $fields.base=$base.Name
        $pr=Invoke-SubmissionApi $auth POST "repos/$repo/pulls" $fields
    }
    if (!$pr.number) { throw 'Check GitHub before submitting again.' }
    if ((Show-Message "Mission submitted. Open pull request #$($pr.number)?" 'YesNo') -eq 'Yes') {
        Start-Process "https://github.com/$repo/pull/$($pr.number)"
    }
    return 0
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
