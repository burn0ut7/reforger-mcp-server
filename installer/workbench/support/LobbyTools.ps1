# Windows-only helpers are installed as ordinary files, never read from a PAK.
function Install-GcLobbyTools([string]$Destination, [string]$Profile) {
    $control = Assert-GcOwnedPath (Join-Path $env:LOCALAPPDATA 'GCWorkbench') $env:LOCALAPPDATA
    $record = Assert-GcOwnedPath (Join-Path $control 'lobby-tools.json') $control
    $folder = Assert-GcOwnedPath (Join-Path $Profile 'GCWorkbench/tools') $Profile
    $names = @('GC-ModLauncher.js', 'GC-ModWorkflow.ps1', 'GC-SubmitMission.ps1', 'GC-Review.ps1', 'GC-GitHub.ps1')
    $previous = @()
    if (Test-Path -LiteralPath $record) {
        $installed = Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
        if ($installed.profile -ne $Profile) { throw 'Uninstall GC tools before changing Workbench profiles.' }
        $previous = @($installed.files)
    }
    foreach ($name in $names) {
        $source = Join-Path $Destination ('payload/lobby2-tools/' + $name)
        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing packaged helper: $name" }
        $target = Assert-GcOwnedPath (Join-Path $folder $name) $Profile
        if (Test-Path -LiteralPath $target) {
            $owned = @($previous | Where-Object name -eq $name)
            if ($owned.Count -ne 1 -or (Get-FileHash -LiteralPath $target).Hash -ne $owned[0].sha256) {
                throw "Unmanaged or modified Git helper preserved: $target"
            }
        }
    }
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $files = @(foreach ($name in $names) {
        $target = Join-Path $folder $name
        Copy-Item -LiteralPath (Join-Path $Destination ('payload/lobby2-tools/' + $name)) -Destination $target
        @{ name = $name; sha256 = (Get-FileHash -LiteralPath $target).Hash }
    })
    @{ profile = $Profile; files = $files } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $record -Encoding UTF8
}

function Uninstall-GcLobbyTools([string]$Control) {
    $record = Assert-GcOwnedPath (Join-Path $Control 'lobby-tools.json') $Control
    if (!(Test-Path -LiteralPath $record)) { return }
    $installed = Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
    $folder = Assert-GcOwnedPath (Join-Path $installed.profile 'GCWorkbench/tools') $installed.profile
    foreach ($file in $installed.files) {
        if ($file.name -notin @('GC-ModLauncher.js', 'GC-ModWorkflow.ps1', 'GC-SubmitMission.ps1', 'GC-Review.ps1', 'GC-GitHub.ps1')) { throw 'Unexpected Git helper in installation record.' }
        $target = Assert-GcOwnedPath (Join-Path $folder $file.name) $installed.profile
        if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -ne $file.sha256) {
            throw "Modified Git helper preserved: $target"
        }
    }
    foreach ($file in $installed.files) {
        $target = Join-Path $folder $file.name
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    Remove-Item -LiteralPath $record
}
