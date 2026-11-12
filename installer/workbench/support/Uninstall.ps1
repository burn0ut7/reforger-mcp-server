param([switch]$Quiet)
. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    $result = Uninstall-GcIntegration
    if ($Quiet) { Write-Output $result } else { Show-Message $result | Out-Null }
}
catch {
    if ($Quiet) { throw }
    Show-Message $_.Exception.Message 'OK' 'Warning' | Out-Null
    exit 1
}
