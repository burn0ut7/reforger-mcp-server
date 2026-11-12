$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LobbyTools.ps1')

function Show-Message([string]$Text, [string]$Buttons = 'OK', [string]$Icon = 'Information') {
    Add-Type -AssemblyName System.Windows.Forms
    return [System.Windows.Forms.MessageBox]::Show($Text, 'GC Workbench integration', $Buttons, $Icon).ToString()
}

function Invoke-GcHelper([string]$Helper, [string[]]$Arguments, [string]$OutputPath, [string]$ErrorPath) {
    # Windows paths cannot contain quotes; quote every argument for spaces.
    if (@($Arguments | Where-Object { $_.Contains('"') -or $_.EndsWith('\') }).Count) { throw 'Unexpected helper argument.' }
    $quoted = @($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $process = Start-Process -FilePath $Helper -ArgumentList $quoted -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $OutputPath -RedirectStandardError $ErrorPath
    return $process.ExitCode
}

function Get-LatestGitInstaller {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/git-for-windows/git/releases/latest' -Headers @{ 'User-Agent' = 'GC-Workbench-Installer' }
    $assets = @($release.assets | Where-Object { $_.name -match '^Git-[0-9][0-9.]*-64-bit\.exe$' })
    if ($assets.Count -ne 1) { throw 'The official Git release does not contain exactly one Windows x64 installer.' }
    $asset = $assets[0]
    if ($asset.browser_download_url -notmatch '^https://github\.com/git-for-windows/git/releases/download/[^/]+/Git-[0-9][0-9.]*-64-bit\.exe$' -or
        $asset.digest -notmatch '^sha256:[0-9a-fA-F]{64}$') { throw 'The official Git release URL or checksum is missing or unexpected.' }
    return $asset
}

function Install-LatestGit {
    $asset = Get-LatestGitInstaller
    $folder = Join-Path ([IO.Path]::GetTempPath()) ('GC-Git-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $folder | Out-Null
    $file = Join-Path $folder $asset.name
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile $file
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $asset.digest.Substring(7)) { throw 'Git download checksum did not match the official release.' }
        if ((Get-AuthenticodeSignature -LiteralPath $file).Status -ne 'Valid') { throw 'Git installer Windows signature is not valid.' }
        # This is the official interactive Git installer; its own wizard requests elevation if needed.
        $process = Start-Process -FilePath $file -ArgumentList '/NORESTART' -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010)) { throw "Git installation did not complete (exit $($process.ExitCode))." }
        return "Git installed using $($asset.name). Open a new terminal to use it."
    }
    finally {
        # Delete only the downloaded file, never a computed directory tree.
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
        if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder }
    }
}


function Assert-GcOwnedPath([string]$Path, [string]$Root) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    if (!$full.StartsWith($base + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Path is outside installation root: $full" }
    $item = $full
    while ($item) {
        if ((Test-Path -LiteralPath $item) -and ((Get-Item -LiteralPath $item -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Refusing linked installation path: $item" }
        $item = Split-Path $item -Parent
    }
    return $full
}

function Remove-GcLegacyStartup {
    $control = Assert-GcOwnedPath (Join-Path $env:LOCALAPPDATA 'GCWorkbench') $env:LOCALAPPDATA
    $automatic = Assert-GcOwnedPath (Join-Path $control 'automatic.json') $control
    $run = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
    Remove-ItemProperty -LiteralPath $run -Name 'GCWorkbench' -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $automatic) { Remove-Item -LiteralPath $automatic }
    $shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Enable GC Workbench.lnk'
    if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut }
}

function Install-GcFiles([string]$Source, [string]$Destination, [string]$WorkbenchExecutable) {
    if (!(Test-Path -LiteralPath $WorkbenchExecutable -PathType Leaf) -or
        [IO.Path]::GetFileName($WorkbenchExecutable) -ne 'ArmaReforgerWorkbenchSteamDiag.exe') { throw 'Select ArmaReforgerWorkbenchSteamDiag.exe from your Workbench installation.' }
    if (!(Test-Path -LiteralPath (Join-Path $Source 'payload/helper/ReforgerMcp.exe')) -or
        !(Test-Path -LiteralPath (Join-Path $Source 'payload/generic/rstauthoring.dll'))) { throw 'Extract the complete ZIP before running Install.bat.' }
    $null = Assert-GcOwnedPath $Destination (Join-Path $env:LOCALAPPDATA 'GCWorkbench/versions')
    if (Test-Path -LiteralPath $Destination) { throw 'The selected installation directory already exists.' }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $Source 'payload') -Destination $Destination -Recurse
    foreach ($name in @('Common.ps1', 'Enable.ps1', 'Uninstall.ps1', 'LobbyTools.ps1')) { Copy-Item -LiteralPath (Join-Path $Source $name) -Destination $Destination }
    $config = @{
        dataDirectory = (Join-Path $Destination 'state')
        workbench = @{ executable = $WorkbenchExecutable; addonDirectories = @(); runtimePort = $null }
    }
    $config | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Destination 'configuration.json') -Encoding UTF8
    return $Destination
}

function Enable-GcStartup([string]$Destination) {
    $control = Assert-GcOwnedPath (Join-Path $env:LOCALAPPDATA 'GCWorkbench') $env:LOCALAPPDATA
    $config = [IO.Path]::GetFullPath((Join-Path $Destination 'configuration.json'))
    $setup = Get-Content -LiteralPath (Join-Path $Destination 'status.json') -Raw | ConvertFrom-Json
    $profile = [IO.Path]::GetFullPath($setup.profileDirectory)
    $script = Assert-GcOwnedPath (Join-Path $profile 'scripts/WorkbenchGame/GCWorkbench/GC_WorkbenchStartup.c') $profile
    $manifest = Assert-GcOwnedPath (Join-Path $control 'installation.json') $control
    if (Test-Path -LiteralPath $manifest) {
        $previous = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        if ($previous.startupPath -ne $script) { throw 'A different Workbench profile is installed. Uninstall that integration before changing profiles.' }
    }
    if (Test-Path -LiteralPath $script) {
        if (!(Test-Path -LiteralPath $manifest)) { throw "Unmanaged startup script preserved: $script" }
        $previous = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        if ($previous.startupPath -ne $script -or (Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash -ne $previous.startupSha256) { throw "Modified startup script preserved: $script" }
    }
    $executable = [IO.Path]::GetFullPath((Join-Path $Destination 'payload/helper/ReforgerMcp.exe')).Replace('\','/')
    $dll = [IO.Path]::GetFullPath((Join-Path $Destination 'payload/generic/rstauthoring.dll')).Replace('\','/')
    $configArgument = $config.Replace('\','/')
    if (($executable + $dll + $configArgument).Contains('"')) { throw 'Unsupported quote in installation path.' }
    $content = @"
// Installed GC Workbench bootstrap. Managed by Install.bat / Uninstall.bat.
[WorkbenchPluginAttribute(name: "GC Workbench integration", wbModules: { "ResourceManager" })]
class GC_WorkbenchStartup : WorkbenchPlugin
{
`tvoid GC_WorkbenchStartup()
`t{
`t`tProcessHandle helper = Workbench.RunProcess("\"$executable\" --config \"$configArgument\" --start-authoring \"$dll\"");
`t`tif (helper)
`t`t`tWorkbench.FinishProcess(helper);
`t}
}
"@
    New-Item -ItemType Directory -Path (Split-Path $script -Parent) -Force | Out-Null
    Install-GcLobbyTools $Destination $profile
    # Deactivate the previous version before replacing its bootstrap. No sign-in task is installed.
    Remove-GcLegacyStartup
    $active = Assert-GcOwnedPath (Join-Path $control 'active.json') $control
    if (Test-Path -LiteralPath $active) { Remove-Item -LiteralPath $active }
    [IO.File]::WriteAllText($script, $content, (New-Object Text.UTF8Encoding($false)))
    @{ startupPath = $script; startupSha256 = (Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash; destination = $Destination; profileDirectory = $profile } |
        ConvertTo-Json | Set-Content -LiteralPath $manifest -Encoding UTF8
    [IO.File]::WriteAllText($active, ($config | ConvertTo-Json))
}

function Uninstall-GcIntegration {
    $control = Assert-GcOwnedPath (Join-Path $env:LOCALAPPDATA 'GCWorkbench') $env:LOCALAPPDATA
    Remove-GcLegacyStartup
    $active = Assert-GcOwnedPath (Join-Path $control 'active.json') $control
    if (Test-Path -LiteralPath $active) { Remove-Item -LiteralPath $active }
    $manifest = Assert-GcOwnedPath (Join-Path $control 'installation.json') $control
    if (Test-Path -LiteralPath $manifest) {
        $installed = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        $expected = Join-Path $installed.profileDirectory 'scripts/WorkbenchGame/GCWorkbench/GC_WorkbenchStartup.c'
        if ($installed.startupPath -ne $expected) { throw 'Installation record has an unexpected startup script path.' }
        $script = Assert-GcOwnedPath $installed.startupPath $installed.profileDirectory
        if (Test-Path -LiteralPath $script) {
            if ((Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash -ne $installed.startupSha256) { throw "Automatic loading is disabled, but the modified startup script was preserved. Remove or relocate it, then run Uninstall.bat again: $script" }
            Remove-Item -LiteralPath $script
            $scriptDirectory = Split-Path $script -Parent
            if (!(Get-ChildItem -LiteralPath $scriptDirectory -Force)) { Remove-Item -LiteralPath $scriptDirectory }
        }
    }
    Uninstall-GcLobbyTools $control
    # A live DLL cannot safely unload. Disable first, then require a normal host exit.
    $loaded = @(Get-Process -Name 'ArmaReforgerWorkbenchSteamDiag' -ErrorAction SilentlyContinue)
    if ($loaded.Count) { throw 'Automatic loading is disabled and its startup script was removed. Close Workbench normally after saving your work, then run Uninstall.bat again to finish removing the installed files.' }
    # Old sign-in watchers exit when their active file disappears; bounded startup helpers do too.
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $helpers = @(Get-Process -Name ReforgerMcp -ErrorAction SilentlyContinue | Where-Object {
            try { $_.Path.StartsWith($control + '\', [StringComparison]::OrdinalIgnoreCase) } catch { $false }
        })
        if (!$helpers.Count) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($helpers.Count) { throw 'Integration helpers are still finishing. Run Uninstall.bat again after they exit; no processes were force-terminated.' }
    if (Test-Path -LiteralPath $control) {
        $verified = Assert-GcOwnedPath $control ([IO.Path]::GetFullPath($env:LOCALAPPDATA))
        if (@(Get-ChildItem -LiteralPath $verified -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Installation contains linked files; preserved without recursive deletion.' }
        Remove-Item -LiteralPath $verified -Recurse -Force
    }
    return 'GC Workbench integration was removed. The shared Reforger MCP adapter, NET API preference, your projects and Git were preserved.'
}
