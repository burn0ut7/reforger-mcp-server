[CmdletBinding()]
param(
    [string]$Lobby2, [string]$GlobalConflictsCore, [string]$GlobalConflictsUnits,
    [string]$GlobalConflictsMissions, [string]$SelectedMods, [switch]$UpdateMods, [string]$MissionWorld, [switch]$SubmitMission
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
. (Join-Path $PSScriptRoot 'GC-Review.ps1')
. (Join-Path $PSScriptRoot 'GC-GitHub.ps1')
[Console]::OutputEncoding = New-Object Text.UTF8Encoding
$env:LC_ALL = 'C'
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'Never'

function Git([string]$root, [string[]]$arguments) {
    $ErrorActionPreference = 'Continue'
    $oldStore=$env:GCM_CREDENTIAL_STORE;$oldTrace=$env:GCM_TRACE_SECRETS
    try {
        $env:GCM_CREDENTIAL_STORE='wincredman';$env:GCM_TRACE_SECRETS='0'
        $output = @(& git.exe -c credential.helper= -c credential.helper=manager -c credential.credentialStore=wincredman -C $root @arguments 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Git failed ($($arguments[0])) in $root. $($output -join [Environment]::NewLine)" }
        $output | ForEach-Object { "$_" }
    } finally {$env:GCM_CREDENTIAL_STORE=$oldStore;$env:GCM_TRACE_SECRETS=$oldTrace}
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
            throw 'This mod has an unfinished update. Ask a mission reviewer to help finish it; your files have been preserved.'
        }
    }
    $root
}

function Confirm-Replacement($name, [string[]]$filesToReplace) {
    $review=Show-GcReview @{
        Title="Update $name"; Section='Local edits overlap this update'; SecondaryAction='Keep my files'
        Fields=@(@{Id='files';Label='Files to replace';Kind='multiline';ReadOnly=$true;Value=($filesToReplace -join "`n")})
        Actions=@(@{Id='backup';Label='Back up my files and update'})
    }
    return $review -and $review.Action -eq 'backup'
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
        $tree = Merge-GcReviewedTree $root $before $target $addon.Name
        if (!$tree) { return 'Cancelled. Your files are unchanged.' }
        try { $target = Git $root @('commit-tree',$tree,'-p',$before,'-p',$target,'-m',"Merge $defaultBranch into $branch") }
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
    $status=Connect-GcGitHub
    if ($status.Connected) {return 0}
    $null=Show-GcReview @{Title='GitHub';PrimaryAction='Close';SecondaryAction='';Items=@(@{Title='Connection';Status=$status.Status;State='warning'})}
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

function Get-GcAddons($Lobby2,$GlobalConflictsCore,$GlobalConflictsUnits,$GlobalConflictsMissions) {
    $addons = @(
        @{Name='Lobby2'; Key='Lobby2'; Project=$Lobby2; Guid='6A115D5DFF7ED870'; Repo='burn0ut7/Lobby2'}
        @{Name='GC Core'; Key='GlobalConflictsCore'; Project=$GlobalConflictsCore; Guid='623555110E2B2CA0'; Repo='Global-Conflicts-ArmA/gc-reforger-core'}
        @{Name='GC Units'; Key='GlobalConflictsUnits'; Project=$GlobalConflictsUnits; Guid='6156F2F771D5D73D'; Repo='Global-Conflicts-ArmA/gc-reforger-units'}
        @{Name='GC Missions'; Key='GlobalConflictsMissions'; Project=$GlobalConflictsMissions; Guid='62D55563549DA97A'; Repo='Global-Conflicts-ArmA/gc-reforger-missions'}
    )
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
    $addons
}

function Invoke-ModWorkflow {
    Add-Type -AssemblyName System.Windows.Forms
    $addons = @(Get-GcAddons $Lobby2 $GlobalConflictsCore $GlobalConflictsUnits $GlobalConflictsMissions)
    if (!(Get-Command git.exe -ErrorAction SilentlyContinue)) {
        if ((Show-Message 'Install Git for Windows?' 'YesNo') -eq 'Yes') {
            Start-Process 'https://git-scm.com/downloads/win'
        }
        return 2
    }
    if ($SubmitMission) {
        $status=Get-GcGitHubStatus
        if (!$status.Connected) {
            $choice=Show-GcReview @{Title='GitHub';PrimaryAction='Sign in on GitHub';Items=@(@{Title='Connection';Status=$status.Status;State='warning'})}
            if (!$choice -or (Connect-GitHub) -ne 0) {return 2}
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
    catch {
        $message=$_.Exception.Message
        if ($message.StartsWith('Git failed')) { $message='Could not prepare the submission. Check your connection and try again.' }
        if ($SubmitMission) {
            try { $null=Show-GcReview @{Title='Submission paused';PrimaryAction='Close';SecondaryAction='';Items=@(@{Title='Your mission files are safe';Status=$message;State='warning'})} }
            catch { $null=Show-Message $message }
        } else { $null=Show-Message $message }
        exit 2
    }
}
