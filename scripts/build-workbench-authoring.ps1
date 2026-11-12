param(
    [Parameter(Mandatory)][string]$QtRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$CMake = 'cmake',
    [string]$Generator = 'Visual Studio 18 2026'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSource = Join-Path $taskRoot 'native/WorkbenchAuthoring'
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$taskQt = (Resolve-Path -LiteralPath $QtRoot).Path
if (Test-Path -LiteralPath $taskOutput) { throw 'Use a new output directory; previous evidence is never overwritten.' }
if (!(Test-Path -LiteralPath (Join-Path $taskQt 'lib/cmake/Qt6/Qt6Config.cmake'))) { throw 'QtRoot must contain the Qt 6.8.3 MSVC x64 development package.' }
$taskCMake = (Get-Command $CMake -ErrorAction Stop).Source
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskSnapshot = Join-Path $taskOutput 'source'
New-Item -ItemType Directory -Path $taskSnapshot | Out-Null
$taskFiles = @(Get-ChildItem -LiteralPath $taskSource -File | Sort-Object Name | ForEach-Object {
    $destination = Join-Path $taskSnapshot $_.Name
    Copy-Item -LiteralPath $_.FullName -Destination $destination
    $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()) {
        throw "Source changed during snapshot: $($_.Name)"
    }
    @{ path = $_.Name; sha256 = $hash }
})
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $taskOutput 'build-workbench-authoring.ps1')
$taskFiles | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'source-hashes.json') -Encoding utf8
$taskBuild = Join-Path $taskOutput 'build'
$taskPackage = Join-Path $taskOutput 'package'
$taskOriginalPath = $env:PATH
try {
    & $taskCMake --version | Set-Content -LiteralPath (Join-Path $taskOutput 'cmake-version.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine CMake version.' }
    & $taskCMake -S $taskSnapshot -B $taskBuild -G $Generator -A x64 "-DCMAKE_PREFIX_PATH=$taskQt" "-DCMAKE_INSTALL_PREFIX=$taskPackage" -DBUILD_TESTING=ON 2>&1 |
        Tee-Object -FilePath (Join-Path $taskOutput 'configure.txt')
    if ($LASTEXITCODE -ne 0) { throw 'CMake configuration failed.' }
    & $taskCMake --build $taskBuild --config Release --parallel 2 2>&1 |
        Tee-Object -FilePath (Join-Path $taskOutput 'build.txt')
    if ($LASTEXITCODE -ne 0) { throw 'DLL build failed.' }
    $taskDependenciesPath = Join-Path $taskBuild 'dependencies.txt'
    $taskDependencies = @([regex]::Matches((Get-Content -LiteralPath $taskDependenciesPath -Raw), '(?im)^\s+([a-z0-9_.-]+\.dll)\s*$') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $taskAllowedDependencies = @('Qt6Core.dll', 'Qt6Gui.dll', 'Qt6Widgets.dll', 'Qt6Network.dll', 'IPHLPAPI.DLL', 'KERNEL32.dll')
    if ($taskDependencies.Count -ne $taskAllowedDependencies.Count -or
        @($taskDependencies | Where-Object { $_ -notin $taskAllowedDependencies }).Count -ne 0) {
        throw "Unexpected DLL dependencies: $($taskDependencies -join ', '). Nothing packaged."
    }
    $env:PATH = (Join-Path $taskQt 'bin') + [IO.Path]::PathSeparator + $taskOriginalPath
    $taskCTest = Join-Path (Split-Path $taskCMake -Parent) 'ctest.exe'
    & $taskCTest --test-dir $taskBuild -C Release --output-on-failure 2>&1 |
        Tee-Object -FilePath (Join-Path $taskOutput 'tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Compatibility tests failed; nothing packaged.' }
    foreach ($source in $taskFiles) {
        if ((Get-FileHash -LiteralPath (Join-Path $taskSnapshot $source.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.sha256) {
            throw "Frozen source changed: $($source.path)"
        }
    }
    & $taskCMake --install $taskBuild --config Release 2>&1 | Tee-Object -FilePath (Join-Path $taskOutput 'install.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Package creation failed.' }
    $taskDll = Join-Path $taskPackage 'generic/rstauthoring.dll'
    $taskManifest = @{
        schema = 1
        state = 'experimental_choices'
        defaultMode = 'diagnostics_only'
        authoringEnabled = $false
        runtimeApproved = $false
        pluginKey = 'rstauthoring'
        qtVersion = '6.8.3'
        dependencies = $taskDependencies
        sourceFiles = $taskFiles
        buildScriptSha256 = (Get-FileHash -LiteralPath (Join-Path $taskOutput 'build-workbench-authoring.ps1') -Algorithm SHA256).Hash.ToLowerInvariant()
        files = @(@{ path = 'generic/rstauthoring.dll'; sha256 = (Get-FileHash -LiteralPath $taskDll -Algorithm SHA256).Hash.ToLowerInvariant() })
        scope = 'Diagnostic default. Explicit choices configuration enables the experimental editor hook; live authoring approval remains required. No drag/drop conversion. Nothing deployed to Workbench.'
    }
    $taskManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskPackage 'manifest.json') -Encoding utf8
    Write-Output "Built Workbench authoring DLL and evidence: $taskOutput"
} catch {
    @{ state = 'failed'; error = $_.Exception.Message; runtimeApproved = $false } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $taskOutput 'failure.json') -Encoding utf8
    throw
} finally {
    $env:PATH = $taskOriginalPath
}
