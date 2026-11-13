# Shared presentation and isolated conflict checks for mission submissions and mod updates.
function Show-GcReview($spec) {
    $registration = Join-Path $env:LOCALAPPDATA 'GCWorkbench/ui-path.txt'
    if (!(Test-Path -LiteralPath $registration)) { throw 'Install GC Tools before continuing.' }
    $ui = [IO.File]::ReadAllText($registration).Trim()
    if (!(Test-Path -LiteralPath $ui -PathType Leaf)) { throw 'GC Tools needs repair. Run Install External Dependencies.' }
    $request = Join-Path ([IO.Path]::GetTempPath()) ('gc-review-' + [guid]::NewGuid() + '.json')
    $result = $request + '.result'
    try {
        [IO.File]::WriteAllText($request, ($spec | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
        $process = Start-Process -FilePath $ui -ArgumentList @('--request', ('"' + $request + '"'), '--result', ('"' + $result + '"')) -Wait -PassThru
        if ($process.ExitCode -eq 2) { return }
        if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $result)) { throw 'The review window could not finish. Your files are safe.' }
        Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
    } finally {
        if ([IO.File]::Exists($request)) { [IO.File]::Delete($request) }
        if ([IO.File]::Exists($result)) { [IO.File]::Delete($result) }
    }
}

function Get-GcTreeFile($root, $tree, $path) {
    $entry = @(Git $root @('-c','core.quotePath=false','--literal-pathspecs','ls-tree',$tree,'--',$path))
    if (!$entry.Count) { return @{ Missing=$true; Text='Deleted file' } }
    if ($entry.Count -ne 1 -or $entry[0] -notmatch '^(100644|100755) blob ([a-f0-9]{40,64})\t') {
        throw "This file needs a mission reviewer: $path. No files were replaced."
    }
    $mode=$Matches[1]; $oid=$Matches[2]
    $size = [long](Git $root @('cat-file','-s',$oid))
    if ($size -gt 12000) { throw "This file is too large for the prototype review: $path. No files were replaced." }
    $text = (Git $root @('cat-file','blob',$oid)) -join "`n"
    if ($text.Contains([string][char]0) -or $text.Contains([string][char]0xFFFD)) { throw "This file needs a mission reviewer: $path. No files were replaced." }
    @{ Missing=$false; Mode=$mode; Oid=$oid; Text=$text }
}

# merge-tree writes objects only. Neither conflict detection nor choices touch the
# working tree or real index. Every conflicting file requires an explicit choice.
function Merge-GcReviewedTree($root, $mine, $shared, $label, $mergeBase = '') {
    $arguments=@('-c','core.quotePath=false','-c','merge.renames=false','merge-tree','--write-tree','--name-only','--no-messages')
    if ($mergeBase) { $arguments += "--merge-base=$mergeBase" }
    $previous=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        $output=@(& git.exe -C $root @arguments $mine $shared 2>&1 | ForEach-Object { "$_" })
        $code=$LASTEXITCODE
    } finally { $ErrorActionPreference=$previous }
    if ($code -notin 0,1 -or !$output.Count -or $output[0] -notmatch '^[a-f0-9]{40,64}$') { throw 'Could not check conflicts. Update Git for Windows and try again.' }
    if ($code -eq 0) { return $output[0] }
    $paths=@($output | Select-Object -Skip 1 | Where-Object { $_ })
    if (!$paths.Count) { throw 'A conflict needs a mission reviewer. No files were replaced.' }
    $index=Join-Path ([IO.Path]::GetTempPath()) ('gc-merge-' + [guid]::NewGuid() + '.index')
    $previousIndex=$env:GIT_INDEX_FILE
    try {
        $env:GIT_INDEX_FILE=$index
        $null=Git $root @('read-tree',$output[0])
        foreach ($path in $paths) {
            if ($path -match '["\r\n\t]' -or $path.StartsWith('/') -or $path -match '(^|/)\.\.(/|$)') { throw 'A filename needs a mission reviewer. No files were replaced.' }
            $local=Get-GcTreeFile $root $mine $path
            $remote=Get-GcTreeFile $root $shared $path
            if ($local.Missing -and $remote.Missing) { throw "A folder conflict needs a mission reviewer: $path. No files were replaced." }
            $review=Show-GcReview @{
                Title='Resolve conflicting file'; Section=$path; SecondaryAction='Cancel'
                Fields=@(@{Id='mine';Label='My version';Kind='multiline';Value=$local.Text;ReadOnly=$true},@{Id='shared';Label='Shared version';Kind='multiline';Value=$remote.Text;ReadOnly=$true})
                Actions=@(@{Id='mine';Label='Use my file'},@{Id='shared';Label='Use shared file'})
            }
            if (!$review) { return }
            if ($review.Action -notin 'mine','shared') { throw 'Unknown conflict choice.' }
            $chosen=if ($review.Action -eq 'mine') {$local} else {$remote}
            if ($chosen.Missing) { $null=Git $root @('--literal-pathspecs','update-index','--force-remove','--',$path) }
            else { $null=Git $root @('update-index','--add','--cacheinfo',$chosen.Mode,$chosen.Oid,$path) }
        }
        Git $root @('write-tree')
    } finally {
        $env:GIT_INDEX_FILE=$previousIndex
        if ([IO.File]::Exists($index)) { [IO.File]::Delete($index) }
    }
}
