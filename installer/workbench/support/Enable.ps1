param([switch]$Quiet)
. (Join-Path $PSScriptRoot 'Common.ps1')
if (!$Quiet -and (Show-Message "Open your GC project in Workbench, stop Play, and save your work. Keep only one Workbench instance open.`n`nClick OK to enable string choices. Setup may reload scripts and enables Workbench's NET API. Its listener can be reachable on your network." 'OKCancel' 'Information') -ne 'OK') { exit 0 }
$log = Join-Path $PSScriptRoot 'setup.log'
try {
    $configPath = Join-Path $PSScriptRoot 'configuration.json'
    $helper = Join-Path $PSScriptRoot 'payload/helper/ReforgerMcp.exe'
    # Status is read-only. Adopt the observed port without changing the user's port.
    $statusError = Join-Path $PSScriptRoot 'status-error.log'
    $statusOutput = Join-Path $PSScriptRoot 'status.json'
    $statusExit = Invoke-GcHelper $helper @('--config', $configPath, '--setup-runtime', 'status') $statusOutput $statusError
    $raw = Get-Content -LiteralPath $statusOutput -Raw
    if (!$raw) { throw ('Cannot inspect Workbench. ' + (Get-Content -LiteralPath $statusError -Raw)) }
    $status = ($raw -join "`n") | ConvertFrom-Json
    if (!$status.port -or !$status.processId) { throw 'Workbench did not return a usable NET API port.' }
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $config.workbench.runtimePort = [int]$status.port
    # Pin this observed process so a second Workbench cannot redirect installation.
    $process = Get-Process -Id $status.processId
    $config.workbench | Add-Member -NotePropertyName processId -NotePropertyValue $process.Id -Force
    $config.workbench | Add-Member -NotePropertyName processStartedUtc -NotePropertyValue ($process.StartTime.ToUniversalTime().ToString('o')) -Force
    $sessionConfig = Join-Path $PSScriptRoot ('session-' + [Guid]::NewGuid().ToString('N') + '.json')
    $config | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $sessionConfig -Encoding UTF8
    $loadError = Join-Path $PSScriptRoot 'setup-error.log'
    if ($Quiet) {
        $loadExit = Invoke-GcHelper $helper @('--config', $sessionConfig, '--setup-runtime', 'install') $log $loadError
    } else {
        $loadExit = Invoke-GcHelper $helper @('--config', $sessionConfig, '--enable-authoring', (Join-Path $PSScriptRoot 'payload/generic/rstauthoring.dll')) $log $loadError
    }
    if ($loadExit -ne 0) { throw ((Get-Content -LiteralPath $log -Raw) + (Get-Content -LiteralPath $loadError -Raw)) }
    $config.workbench.PSObject.Properties.Remove('processId')
    $config.workbench.PSObject.Properties.Remove('processStartedUtc')
    $config | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $configPath -Encoding UTF8
    if (!$Quiet) { Show-Message 'GC string choices are enabled. Reselect your component to refresh its properties.' | Out-Null }
}
catch {
    if ($Quiet) { throw }
    Show-Message ("Setup did not complete: " + $_.Exception.Message + "`n`nDetails: $log`nIf loading was dispatched, save and restart Workbench before trying again.") 'OK' 'Error' | Out-Null
    exit 1
}
