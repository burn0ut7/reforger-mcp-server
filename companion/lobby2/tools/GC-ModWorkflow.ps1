[CmdletBinding()]
param(
    [string]$Lobby2, [string]$GlobalConflictsCore, [string]$GlobalConflictsUnits,
    [string]$GlobalConflictsMissions, [string]$SelectedMods, [switch]$UpdateMods, [string]$MissionWorld, [switch]$SubmitMission
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding
$env:LC_ALL = 'C'
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'

function Git([string]$root, [string[]]$arguments) {
    $ErrorActionPreference = 'Continue'
    $output = @(& git.exe -C $root @arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Git failed ($($arguments[0])) in $root. $($output -join [Environment]::NewLine)" }
    $output | ForEach-Object { "$_" }
}

function Get-Checkout([string]$project, [string]$guid, [ref]$desktopPending) {
    if (!$project -or !(Test-Path -LiteralPath $project -PathType Leaf)) { throw "Mod folder not found." }
    if ((Get-Content -LiteralPath $project -Raw) -notmatch ('(?m)^\s*GUID "' + $guid + '"')) { throw 'Wrong mod folder.' }
    $root = [IO.Path]::GetFullPath((Split-Path -LiteralPath $project))
    $top = Git $root @('rev-parse','--show-toplevel')
    if ([IO.Path]::GetFullPath($top) -ne [IO.Path]::GetFullPath($root)) { throw 'This mod is not set up for updates.' }
    foreach ($marker in 'MERGE_HEAD','CHERRY_PICK_HEAD','REVERT_HEAD','rebase-apply','rebase-merge','sequencer','BISECT_START') {
        $path = Git $root @('rev-parse','--path-format=absolute','--git-path',$marker)
        if (Test-Path -LiteralPath $path) {
            if ($desktopPending) { $desktopPending.Value = $true }
            if ($marker -eq 'MERGE_HEAD') { throw 'Finish the current merge first.' }
            throw 'Finish the other Git task first.'
        }
    }
    $root
}

function Confirm-Replacement($name, [string[]]$filesToReplace) {
    $form = New-Object Windows.Forms.Form
    try {
        $form.Text = "Update $name?"
        $form.StartPosition = 'CenterScreen'
        $form.ClientSize = New-Object Drawing.Size(700,460)
        $form.MinimumSize = New-Object Drawing.Size(540,380)
        $description = New-Object Windows.Forms.Label
        $description.Dock = 'Top'
        $description.Height = 52
        $description.Padding = New-Object Windows.Forms.Padding(10)
        $description.Text = "The update would replace your edits to these files. Back them up and update?"
        $files = New-Object Windows.Forms.TextBox
        $files.Multiline = $true
        $files.ReadOnly = $true
        $files.WordWrap = $false
        $files.ScrollBars = 'Both'
        $files.Dock = 'Fill'
        $files.Lines = $filesToReplace
        $buttons = New-Object Windows.Forms.FlowLayoutPanel
        $buttons.Dock = 'Bottom'
        $buttons.FlowDirection = 'RightToLeft'
        $buttons.Height = 42
        $ignore = New-Object Windows.Forms.Button
        $ignore.Text = 'Skip mod'
        $ignore.DialogResult = 'Cancel'
        $discard = New-Object Windows.Forms.Button
        $discard.Text = 'Back up and update'
        $discard.AutoSize = $true
        $discard.DialogResult = 'Yes'
        $buttons.Controls.AddRange(@($ignore,$discard))
        $form.Controls.AddRange(@($files,$description,$buttons))
        $form.AcceptButton = $ignore
        $form.CancelButton = $ignore
        $form.ActiveControl = $ignore
        return $form.ShowDialog() -eq 'Yes'
    } finally { $form.Dispose() }
}

function Open-BranchHelp($root, $branch, $before, $incoming, [ref]$filesChanged, [ref]$desktopPending) {
    $desktop = Get-Command github.bat -ErrorAction SilentlyContinue
    if (!$desktop) {
        if ((Show-Message "Your edits need review. Get GitHub Desktop?" 'YesNo') -eq 'Yes') {
            Start-Process 'https://desktop.github.com/download/'
        }
        return 'Skipped. Your edits are safe.'
    }
    $dirty = @(Git $root @('status','--porcelain','--untracked-files=all')).Count -gt 0
    $question = "This update conflicts with $branch. Resolve it in GitHub Desktop?"
    if ($dirty) { $question = "Commit your edits before updating $branch. Open GitHub Desktop?" }
    if ((Show-Message $question 'YesNo') -ne 'Yes') { return 'Skipped. Your edits are safe.' }
    if ((Git $root @('symbolic-ref','--short','HEAD')) -cne $branch -or (Git $root @('rev-parse','HEAD')) -ne $before) { throw 'Mod changed. Try again.' }
    if (!$dirty) {
        if (Git $root @('status','--porcelain','--untracked-files=all')) { throw 'Mod changed. Try again.' }
        try {
            $null = Git $root @('merge','--ff','--no-squash','--commit','--no-edit','--no-autostash','--no-overwrite-ignore',$incoming)
            $filesChanged.Value = $true
            return 'Updated.'
        } catch {
            if (!(Git $root @('ls-files','--unmerged'))) { throw }
            $filesChanged.Value = $true
        }
    }
    $desktopPending.Value = $true
    Push-Location -LiteralPath $root
    try {
        $null = & $desktop.Source
        if ($LASTEXITCODE -ne 0) { throw "Open this folder in GitHub Desktop: $root" }
    } finally { Pop-Location }
    if ($dirty) { return 'Commit your edits in Desktop, then update again.' }
    return 'Finish the merge in GitHub Desktop.'
}

function Get-ModBase($root, $repo) {
    $lines = @(Git $root @('ls-remote','--symref',"https://github.com/$repo.git",'HEAD') | Where-Object { $_ -cmatch '\tHEAD$' })
    if ($lines.Count -ne 2 -or $lines[0] -notmatch '^ref: refs/heads/(.+)\s+HEAD$') { throw "Could not check $repo." }
    $name = $Matches[1]
    if ($lines[1] -notmatch '^([a-f0-9]{40,64})\s+HEAD$') { throw "Could not check $repo." }
    @{ Name=$name; Commit=$Matches[1] }
}

function Update-Mod($addon, [ref]$filesChanged, [ref]$desktopPending, [switch]$ForSubmission) {
    $root = Get-Checkout $addon.Project $addon.Guid $desktopPending
    $branch = Git $root @('symbolic-ref','--short','HEAD')
    $url = "https://github.com/$($addon.Repo).git"
    $upstream = Get-ModBase $root $addon.Repo
    $defaultBranch = $upstream.Name
    $personal = $branch -cne $defaultBranch
    $target = $upstream.Commit
    $incoming = $target
    $before = Git $root @('rev-parse','HEAD')
    if ($before -eq $target) { return 'Up to date.' }
    $null = Git $root @('fetch','--quiet','--no-tags',$url,"refs/heads/$defaultBranch")
    $base = Git $root @('merge-base',$before,$target)
    if ($base -eq $target) { return 'Up to date.' }
    if ($base -ne $before) {
        if (!$personal) { return 'Skipped to keep your saved work.' }
        # Merge the committed histories first, without touching the author's index or files.
        # Advancing to the resulting two-parent commit uses the same protected update below.
        $ErrorActionPreference = 'Continue'
        $tree = @(& git.exe -C $root merge-tree --write-tree --no-messages $before $target 2>&1)
        $mergeResult = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($mergeResult -eq 1) {
            if ($ForSubmission) { return 'Resolve the branch conflict first.' }
            return Open-BranchHelp $root $branch $before $target $filesChanged $desktopPending
        }
        if ($mergeResult -ne 0 -or $tree.Count -ne 1 -or $tree[0] -notmatch '^[0-9a-f]{40,64}$') { throw 'Could not check this merge. Update Git for Windows and try again.' }
        try { $target = Git $root @('commit-tree',$tree[0],'-p',$before,'-p',$target,'-m',"Merge $defaultBranch into $branch") }
        catch { throw 'Could not save the merge. Check your Git name, email and signing settings.' }
    }
    $merge = @('-c','core.quotePath=false','merge','--ff-only','--no-squash','--no-autostash','--no-overwrite-ignore','--quiet',$target)
    if ((Git $root @('symbolic-ref','--short','HEAD')) -cne $branch -or (Git $root @('rev-parse','HEAD')) -ne $before) { throw 'Mod changed. Try again.' }
    try {
        $null = Git $root $merge
        $filesChanged.Value = $true
        return 'Updated.'
    } catch {
        # Only offer replacement for paths Git explicitly refused to overwrite.
        $reason = $_.Exception.Message
        if ($reason -notmatch 'would be overwritten by merge:|would be removed by merge:') { throw }
        if ($ForSubmission) { return 'Local changes block this update.' }
        $paths = @([regex]::Matches($reason,'(?m)^\t([^\r\n]+)') | ForEach-Object { $_.Groups[1].Value })
        if (!$paths.Count) { return 'Skipped to protect your files.' }
    }
    foreach ($path in $paths) {
        # Quoted names, folders and ignored files need manual handling.
        if ($path.StartsWith('"') -or $path.EndsWith('/') -or
            !(Git $root @('--literal-pathspecs','status','--porcelain','--untracked-files=all','--',$path)) -or
            (Test-Path -LiteralPath (Join-Path $root $path) -PathType Container)) { return 'Skipped to protect your files.' }
    }
    if ($personal) { return Open-BranchHelp $root $branch $before $incoming $filesChanged $desktopPending }
    if (!(Confirm-Replacement $addon.Name $paths)) { return 'Skipped. Your edits are safe.' }
    $null = Get-Checkout $addon.Project $addon.Guid
    if ((Git $root @('symbolic-ref','--short','HEAD')) -cne $branch -or (Git $root @('rev-parse','HEAD')) -ne $before) { throw 'Mod changed. Try again.' }

    $label = "GC update $([guid]::NewGuid())"
    # A failed backup can already have changed files. Require an editor refresh.
    $filesChanged.Value = $true
    $null = Git $root (@('--literal-pathspecs','-c','user.name=GC Workbench','-c','user.email=workbench@localhost','stash','push','--quiet','--include-untracked','--message',$label,'--') + $paths)
    $backup = Git $root @('stash','list','-1','--format=%h %gs')
    if (!$backup -or !$backup.EndsWith($label)) { throw 'Backup could not be confirmed. Check the mod before retrying.' }
    $id = $backup.Split(' ')[0]
    try { $null = Git $root $merge }
    catch { return "Update stopped. Your edits are in backup $id." }
    return "Updated. Your edits are in backup $id."
}
function Show-Message([string]$message, [string]$buttons = 'OK') {
    [Windows.Forms.MessageBox]::Show($message,'GC Git', $buttons)
}

function Connect-GitHub {
    $setupUrl = 'https://docs.github.com/en/get-started/git-basics/caching-your-github-credentials-in-git'
    $reason = 'Install Git for Windows with Git Credential Manager to connect an account.'
    $previous = $env:GCM_INTERACTIVE
    try {
        $null = Get-Command git.exe -ErrorAction Stop
        # Windows PowerShell treats native stderr as error records. GCM can write
        # browser instructions there on success, so use its exit code for the result.
        $ErrorActionPreference = 'Continue'
        $null = & git.exe credential-manager github login --help 2>&1
        if ($LASTEXITCODE -eq 0) {
            $reason = 'Sign-in cancelled.'
            $env:GCM_INTERACTIVE = 'true'
            $null = & git.exe credential-manager github login --browser 2>&1
            if ($LASTEXITCODE -eq 0) {
                $null = Show-Message 'Signed in.'
                return 0
            }
        }
    } catch {
        # Show setup guidance without exposing authentication output in Workbench.
    } finally {
        $env:GCM_INTERACTIVE = $previous
    }
    if ((Show-Message "$reason Open GitHub's setup guide?" 'YesNo') -eq 'Yes') {
        Start-Process $setupUrl
    }
    return 2
}

function Read-MissionBranch([string]$current, [string]$world) {
    Add-Type -AssemblyName Microsoft.VisualBasic
    $slug = ([IO.Path]::GetFileNameWithoutExtension($world).ToLowerInvariant() -replace '[^a-z0-9]+','-').Trim('-')
    [Microsoft.VisualBasic.Interaction]::InputBox("Name your mission branch. Cancel keeps $current.",'GC Mission Branch',"mission/$slug")
}

function New-MissionBranch($root, $current, $world) {
    $branch = Read-MissionBranch $current $world
    if (!$branch) { return $false }
    if ($branch -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._/-]*$') { throw 'Use letters, numbers, slashes, dots, underscores or hyphens in the branch name.' }
    $null = Git $root @('check-ref-format','--branch',$branch)
    if ((Git $root @('symbolic-ref','--short','HEAD')) -cne $current) { throw 'Branch changed. Try again.' }
    $null = Git $root @('switch','--quiet','-c',$branch)
    if ((Git $root @('symbolic-ref','--short','HEAD')) -cne $branch) { throw 'Could not verify the new branch.' }
    return $true
}

function Invoke-ModWorkflow {
    Add-Type -AssemblyName System.Windows.Forms
    $addons = @(
        @{Name='Lobby2'; Key='Lobby2'; Project=$Lobby2; Guid='6A115D5DFF7ED870'; Repo='burn0ut7/Lobby2'}
        @{Name='GC Core'; Key='GlobalConflictsCore'; Project=$GlobalConflictsCore; Guid='623555110E2B2CA0'; Repo='Global-Conflicts-ArmA/gc-reforger-core'}
        @{Name='GC Units'; Key='GlobalConflictsUnits'; Project=$GlobalConflictsUnits; Guid='6156F2F771D5D73D'; Repo='Global-Conflicts-ArmA/gc-reforger-units'}
        @{Name='GC Missions'; Key='GlobalConflictsMissions'; Project=$GlobalConflictsMissions; Guid='62D55563549DA97A'; Repo='Global-Conflicts-ArmA/gc-reforger-missions'}
    )
    if (!(Get-Command git.exe -ErrorAction SilentlyContinue)) {
        if ((Show-Message 'Install Git for Windows?' 'YesNo') -eq 'Yes') {
            Start-Process 'https://git-scm.com/downloads/win'
        }
        return 2
    }
    $accounts = @()
    try { $accounts = @(& git.exe credential-manager github list 2>$null) } catch {}
    if ($LASTEXITCODE -ne 0 -or !$accounts.Count) {
        if ($SubmitMission) {
            $answer = Show-Message 'Sign in to GitHub to submit your mission?' 'YesNo'
            if ($answer -ne 'Yes') { return 2 }
        } else {
            $answer = Show-Message 'Sign in to GitHub? Choose No to continue without signing in.' 'YesNoCancel'
        }
        if ($answer -eq 'Cancel') { return 2 }
        if ($answer -eq 'Yes' -and (Connect-GitHub) -ne 0) { return 2 }
    }
    # Workbench only resolves mounts loaded in the current project. Source checkouts
    # installed alongside Lobby2 can still be updated after their GUID is verified.
    if (!$Lobby2 -or !(Test-Path -LiteralPath $Lobby2 -PathType Leaf)) {
        throw 'Open the Lobby2 source project before using GC Git tools.'
    }
    $addonsDirectory = Split-Path -Parent (Split-Path -Parent ([IO.Path]::GetFullPath($Lobby2)))
    foreach ($addon in $addons) {
        if (!$addon.Project) {
            $folder = ($addon.Repo -split '/')[-1]
            $addon.Project = Join-Path $addonsDirectory "$folder/addon.gproj"
        }
    }
    if ($SubmitMission) {
        . (Join-Path $PSScriptRoot 'GC-SubmitMission.ps1')
        return Invoke-MissionSubmission $addons $MissionWorld
    }
    if ($UpdateMods -and !$SelectedMods) { $SelectedMods = $addons.Key -join ',' }
    if ($SelectedMods) {
        $selectedKeys = $SelectedMods -split ','
        $rows = @()
        $changed = $false
        $desktopPending = $false
        foreach ($addon in $addons) {
            if ($addon.Key -notin $selectedKeys) { continue }
            try {
                $result = Update-Mod $addon ([ref]$changed) ([ref]$desktopPending)
            } catch {
                $result = $_.Exception.Message
                if ($result.StartsWith('Git failed')) { $result = 'Could not update. Check your connection and mod folder.' }
            }
            $rows += "$($addon.Name): $result"
        }
        if ($changed -and !$desktopPending) { $rows += 'Reload Workbench to use the changes.' }
        $null = Show-Message ($rows -join [Environment]::NewLine)
        if ($changed -or $desktopPending) { return 2 }
    }
    if ($MissionWorld) {
        $missions = $addons | Where-Object Key -eq GlobalConflictsMissions
        $root = Get-Checkout $missions.Project $missions.Guid
        $world = (Get-Item -LiteralPath $MissionWorld).FullName
        if (!$world.StartsWith($root.TrimEnd('\','/') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Save your mission in GC Missions first.' }
        $current = Git $root @('symbolic-ref','--short','HEAD')
        $null = New-MissionBranch $root $current $world
    }
    return 0
}

if ($MyInvocation.InvocationName -ne '.') {
    try { exit (Invoke-ModWorkflow) }
    catch { $null = Show-Message "$_"; exit 2 }
}
