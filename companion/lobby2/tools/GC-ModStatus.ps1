param([string]$Lobby2,[string]$GlobalConflictsCore,[string]$GlobalConflictsUnits,[string]$GlobalConflictsMissions,
    [ValidateSet('Lobby2','GlobalConflictsCore','GlobalConflictsUnits','GlobalConflictsMissions')][string]$Mod)
$projects=@{Lobby2=$Lobby2;GlobalConflictsCore=$GlobalConflictsCore;GlobalConflictsUnits=$GlobalConflictsUnits;GlobalConflictsMissions=$GlobalConflictsMissions}
. (Join-Path $PSScriptRoot 'GC-ModWorkflow.ps1')

function Get-GcModStatus($addon) {
    try {
        if (!$addon.Project -or !(Test-Path -LiteralPath $addon.Project -PathType Leaf)) {return @{Status='Mod folder not found';UpdateNeeded=$false}}
        $root=Get-Checkout $addon.Project $addon.Guid
        $branch=Git $root @('symbolic-ref','--short','HEAD')
        $before=Git $root @('rev-parse','HEAD')
        $upstream=Get-ModBase $root $addon.Repo
        if ($before -eq $upstream.Commit) {return @{Status='Up to date';UpdateNeeded=$false}}
        # Download history only, without moving branches, touching the index, or changing files.
        $null=Git $root @('fetch','--quiet','--no-tags','--no-write-fetch-head',('https://github.com/'+$addon.Repo+'.git'),('refs/heads/'+$upstream.Name))
        $base=Git $root @('merge-base',$before,$upstream.Commit)
        if ((Git $root @('rev-parse','HEAD')) -ne $before -or (Git $root @('symbolic-ref','--short','HEAD')) -cne $branch) {return @{Status='Mod changed - refresh';UpdateNeeded=$false}}
        if ($base -eq $upstream.Commit) {return @{Status='Up to date';UpdateNeeded=$false}}
        if ($base -ne $before -and $branch -ceq $upstream.Name) {return @{Status='Local changes need review';UpdateNeeded=$false}}
        if ($base -ne $before) {return @{Status='Update available - review may be needed';UpdateNeeded=$true}}
        return @{Status='Update available';UpdateNeeded=$true}
    } catch {
        return @{Status='Unable to check - refresh';UpdateNeeded=$false}
    }
}
if ($MyInvocation.InvocationName -ne '.') {
    try {
        $addon=@(Get-GcAddons @projects) | Where-Object Key -eq $Mod
        if (!$addon) {throw 'Unknown mod'}
        Get-GcModStatus $addon | ConvertTo-Json -Compress
    } catch { @{Status='Mod folder not found';UpdateNeeded=$false} | ConvertTo-Json -Compress }
}
