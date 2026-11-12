. (Join-Path $PSScriptRoot 'Common.ps1')
$results = New-Object 'System.Collections.Generic.List[string]'
$failed = $false
if ((Show-Message "Install GC Workbench integration for this Windows user?`n`nLoads automatically whenever you open Workbench. Open your GC project, stop Play and save work before installing. Requires the supported Workbench build and your GC addon." 'YesNo' 'Question') -eq 'Yes') {
    try {
        Add-Type -AssemblyName System.Windows.Forms
        $picker = New-Object System.Windows.Forms.OpenFileDialog
        $picker.Title = 'Select your Workbench executable'
        $picker.Filter = 'Workbench|ArmaReforgerWorkbenchSteamDiag.exe'
        if ($picker.ShowDialog() -ne 'OK') { throw 'Workbench installation canceled.' }
        $destination = Join-Path $env:LOCALAPPDATA ('GCWorkbench/versions/' + [Guid]::NewGuid().ToString('N'))
        Install-GcFiles $PSScriptRoot $destination $picker.FileName | Out-Null
        & (Join-Path $destination 'Enable.ps1') -Quiet
        Enable-GcStartup $destination
        $results.Add("GC integration installed. Save and close Workbench, then open it normally. Loading starts from Workbench itself; nothing runs at Windows sign-in.`nFiles: $destination")
    }
    catch { $failed = $true; $results.Add('GC integration: ' + $_.Exception.Message) }
}
if ((Show-Message 'Install or update Git from the latest official Git for Windows release? This opens the official Git setup wizard.' 'YesNo' 'Question') -eq 'Yes') {
    try { $results.Add((Install-LatestGit)) }
    catch { $failed = $true; $results.Add('Git: ' + $_.Exception.Message) }
}
if ($results.Count -eq 0) { $results.Add('Nothing was installed.') }
$icon = 'Information'
if ($failed) { $icon = 'Warning' }
Show-Message ($results -join "`n`n") 'OK' $icon | Out-Null
if ($failed) { exit 1 }

