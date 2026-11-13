param([switch]$Preview)
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
    $destination = Join-Path ([IO.Path]::GetTempPath()) ('GCWorkbench-' + [Guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $archive -DestinationPath $destination
    if ($Preview) {
        Start-Process -FilePath (Join-Path $destination 'support/payload/helper/GCWorkbench.UI.exe') -ArgumentList '--preview'
    } else {
        & (Join-Path $destination 'support/Install.ps1')
    }
} catch {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Install External Dependencies', 'OK', 'Error') | Out-Null
    exit 1
}
