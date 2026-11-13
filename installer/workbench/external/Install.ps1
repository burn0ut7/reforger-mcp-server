param([switch]$Preview, [switch]$Tools)
$ErrorActionPreference = 'Stop'
try {
    # Workbench may inherit another PowerShell version's module search path.
    # Load the modules belonging to the host that is actually running this script.
    Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
    Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Archive/Microsoft.PowerShell.Archive.psd1') -ErrorAction Stop
    $archive = Join-Path $PSScriptRoot 'GCWorkbench.zip'
    $expected = (Get-Content -LiteralPath ($archive + '.sha256') -Raw).Split(' ')[0].Trim()
    if ($expected -notmatch '^[0-9a-fA-F]{64}$' -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expected) {
        throw 'The external dependency package is incomplete or changed. Restore external/workbench from the Lobby2 source checkout.'
    }
    if ($Tools -or $Preview) {
        # Reuse one completed extraction per package instead of unpacking on every click.
        $cache = Join-Path $env:LOCALAPPDATA 'GCWorkbench/ui'
        $destination = Join-Path $cache $expected.ToLowerInvariant()
        $mutex = New-Object Threading.Mutex($false, ('Local\GCWorkbench-UI-' + $expected))
        $locked = $false
        try {
            try { $locked = $mutex.WaitOne(60000) } catch [Threading.AbandonedMutexException] { $locked = $true }
            if (!$locked) { throw 'GC Tools is still preparing. Try again shortly.' }
            if (!(Test-Path -LiteralPath (Join-Path $destination 'complete'))) {
                Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
                [IO.File]::WriteAllText((Join-Path $destination 'complete'), $expected)
            }
        } finally {
            if ($locked) { $mutex.ReleaseMutex() }
            $mutex.Dispose()
        }
    } else {
        $destination = Join-Path ([IO.Path]::GetTempPath()) ('GCWorkbench-' + [Guid]::NewGuid().ToString('N'))
        Expand-Archive -LiteralPath $archive -DestinationPath $destination
    }
    if ($Tools) {
        # Bootstrap once for source checkouts that have not installed the integration yet.
        $ui = Join-Path $destination 'support/payload/helper/GCWorkbench.UI.exe'
        [IO.File]::WriteAllText((Join-Path $env:LOCALAPPDATA 'GCWorkbench/ui-path.txt'), $ui, [Text.Encoding]::Unicode)
        $picker = Start-Process -FilePath $ui -ArgumentList '--tools' -Wait -PassThru
        exit $picker.ExitCode
    } elseif ($Preview) {
        Start-Process -FilePath (Join-Path $destination 'support/payload/helper/GCWorkbench.UI.exe') -ArgumentList '--preview'
    } else {
        & (Join-Path $destination 'support/Install.ps1')
    }
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Install External Dependencies', 'OK', 'Error') | Out-Null
    exit 1
}
