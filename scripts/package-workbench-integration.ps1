param(
    [Parameter(Mandatory)][string]$NativePackage,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$LobbyDirectory,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$native = [IO.Path]::GetFullPath($NativePackage)
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Choose a new output directory.' }
foreach ($file in @('generic/rstauthoring.dll', 'manifest.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $native $file))) { throw "Native build package is missing $file" }
}
$nativeManifest = Get-Content -LiteralPath (Join-Path $native 'manifest.json') -Raw | ConvertFrom-Json
$nativeDll = @($nativeManifest.files | Where-Object path -eq 'generic/rstauthoring.dll')
if ($nativeDll.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $native 'generic/rstauthoring.dll')).Hash -ne $nativeDll[0].sha256) {
    throw 'Native DLL does not match its build manifest.'
}
$nativeSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'native/WorkbenchAuthoring') -File)
if (@($nativeManifest.sourceFiles).Count -ne $nativeSources.Count) { throw 'Native source inventory changed; rebuild before packaging.' }
foreach ($source in $nativeSources) {
    $record = @($nativeManifest.sourceFiles | Where-Object path -eq $source.Name)
    if ($record.Count -ne 1 -or (Get-FileHash -LiteralPath $source.FullName).Hash -ne $record[0].sha256) {
        throw "Native source changed; rebuild before packaging: $($source.Name)"
    }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$support = Join-Path $destination 'support'
New-Item -ItemType Directory -Path $support -Force | Out-Null
foreach ($file in @('Install.bat', 'Uninstall.bat', 'README.txt', 'support/Common.ps1', 'support/Enable.ps1', 'support/Install.ps1', 'support/Uninstall.ps1', 'support/LobbyTools.ps1')) {
    Copy-Item -LiteralPath (Join-Path $root ('installer/workbench/' + $file)) -Destination (Join-Path $destination $file)
}
$helper = Join-Path $support 'payload/helper'
New-Item -ItemType Directory -Path (Join-Path $support 'payload/lobby2-tools'),(Join-Path $support 'payload/generic') -Force | Out-Null
foreach ($file in @('GC-ModLauncher.js', 'GC-ModWorkflow.ps1', 'GC-SubmitMission.ps1', 'GC-Review.ps1', 'GC-GitHub.ps1')) {
    Copy-Item -LiteralPath (Join-Path $root ('companion/lobby2/tools/' + $file)) -Destination (Join-Path $support ('payload/lobby2-tools/' + $file))
}
& $Dotnet publish (Join-Path $root 'src/ReforgerMcp/ReforgerMcp.csproj') -c Release -r win-x64 --self-contained true -o $helper -p:OutputType=WinExe -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Helper publish failed.' }
# Share the already bundled Windows desktop runtime with the reusable tool UI.
& $Dotnet publish (Join-Path $root 'companion/lobby2/tools/GCWorkbench.UI/GCWorkbench.UI.csproj') -c Release -r win-x64 --self-contained true -o $helper -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'GC tool UI publish failed.' }
Copy-Item -LiteralPath (Join-Path $native 'generic/rstauthoring.dll') -Destination (Join-Path $support 'payload/generic/rstauthoring.dll')
Copy-Item -LiteralPath (Join-Path $root 'companion/lobby2/workbench/menu-icons.json') -Destination (Join-Path $support 'payload/generic/menu-icons.json')
Copy-Item -LiteralPath (Join-Path $native 'manifest.json') -Destination (Join-Path $support 'payload/native-build.json')
$notices = Join-Path $support 'payload/THIRD-PARTY'
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY') -Destination $notices -Recurse
$runtime = Get-Content -LiteralPath (Join-Path $helper 'ReforgerMcp.runtimeconfig.json') -Raw | ConvertFrom-Json
$assets = Get-Content -LiteralPath (Join-Path $root 'src/ReforgerMcp/obj/project.assets.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $packageName = switch ($framework.name) {
        'Microsoft.NETCore.App' { 'microsoft.netcore.app.runtime.win-x64' }
        'Microsoft.WindowsDesktop.App' { 'microsoft.windowsdesktop.app.runtime.win-x64' }
        'Microsoft.AspNetCore.App' { 'microsoft.aspnetcore.app.runtime.win-x64' }
        default { throw "Unrecognized runtime: $($framework.name)" }
    }
    $package = @($assets.packageFolders.PSObject.Properties.Name | ForEach-Object {
        $candidate = Join-Path $_ ($packageName + '/' + $framework.version)
        if (Test-Path -LiteralPath $candidate) { $candidate }
    }) | Select-Object -First 1
    if (!$package) { throw "Cannot locate notices for $packageName" }
    $licenseFiles = @(Get-ChildItem -LiteralPath $package -File | Where-Object Name -Match '^(LICENSE(\.txt)?|THIRD-PARTY-NOTICES\.TXT)$')
    if (!$licenseFiles.Count) { throw "No runtime licenses found: $package" }
    foreach ($file in $licenseFiles) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $notices ($framework.name + '-' + $file.Name)) }
}
# Keep the precise shipped bytes identifiable; never bundle local configuration or game files.
$files = @(Get-ChildItem -LiteralPath $destination -Recurse -File)
if (@($files | Where-Object { $_.Extension -in @('.pdb', '.pak', '.gproj', '.ent', '.layer') -or $_.Name -eq 'configuration.json' }).Count) { throw 'Unexpected local/game/debug file in package.' }
$manifest = @($files | ForEach-Object {
    @{ path = $_.FullName.Substring($destination.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $support 'files.json') -Encoding UTF8
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath ($destination + '.zip')
$hash = (Get-FileHash -LiteralPath ($destination + '.zip') -Algorithm SHA256).Hash
Set-Content -LiteralPath ($destination + '.zip.sha256') -Value ($hash + '  ' + [IO.Path]::GetFileName($destination) + '.zip') -Encoding ASCII
Write-Output ($destination + '.zip')
if ($LobbyDirectory) {
    $lobby = [IO.Path]::GetFullPath($LobbyDirectory)
    if (!(Test-Path -LiteralPath (Join-Path $lobby 'addon.gproj'))) { throw 'LobbyDirectory must be the Lobby2 source addon.' }
    $external = Join-Path $lobby 'external/workbench'
    if (Test-Path -LiteralPath $external) { throw 'External package already exists; preserve or remove it explicitly before replacing.' }
    New-Item -ItemType Directory -Path $external -Force | Out-Null
    Copy-Item -LiteralPath ($destination + '.zip') -Destination (Join-Path $external 'GCWorkbench.zip')
    Set-Content -LiteralPath (Join-Path $external 'GCWorkbench.zip.sha256') -Value ($hash + '  GCWorkbench.zip') -Encoding ASCII
    foreach ($file in @('Install.js', 'Install.ps1', 'Preview UI.js')) {
        Copy-Item -LiteralPath (Join-Path $root ('installer/workbench/external/' + $file)) -Destination $external
    }
}
