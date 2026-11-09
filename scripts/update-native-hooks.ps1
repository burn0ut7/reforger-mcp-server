param(
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$BaselineManifest,
    [Parameter(Mandatory)][string]$CandidateManifest,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory; evidence is immutable.' }
foreach ($inputPath in @($Configuration, $BaselineManifest, $CandidateManifest)) {
    if (!(Test-Path -LiteralPath $inputPath -PathType Leaf)) { throw "Missing input: $inputPath" }
}
New-Item -ItemType Directory -Path $output | Out-Null
$snapshot = Join-Path $output 'source'
# Freeze the actual working files, including uncommitted changes, before building.
# Do not copy configuration, credentials, game binaries, caches or build products.
$inputs = @(Get-ChildItem -LiteralPath (Join-Path $root 'src'), (Join-Path $root 'adapters'), (Join-Path $root 'tests/NativeHookUpdate'), (Join-Path $root 'tests/WorkbenchMaintenance'), (Join-Path $root 'tests/WorkbenchFocus') -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.java','.json','.c') } |
    Sort-Object FullName)
$sources = @($inputs | ForEach-Object {
    $relative = [IO.Path]::GetRelativePath($root, $_.FullName)
    $destination = Join-Path $snapshot $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination
    $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()) { throw "Source changed during snapshot: $relative" }
    @{ path = $relative; sha256 = $hash }
})
$sources | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'source-hashes.json') -Encoding utf8
$artifacts = Join-Path $output 'build'
$server = Join-Path $output 'server'
$request = @{
    sourceRoot = $snapshot
    baselineManifest = [IO.Path]::GetFullPath($BaselineManifest)
    candidateManifest = [IO.Path]::GetFullPath($CandidateManifest)
    outputDirectory = Join-Path $output 'discovery'
}
$request | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'request.json') -Encoding utf8
& $Dotnet --info | Set-Content -LiteralPath (Join-Path $output 'dotnet-info.txt')
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine .NET toolchain.' }
# Stable source paths in compiler output; never overwrite the running server.
$mapping = "-p:PathMap=$output=/_/run"
& $Dotnet run --project (Join-Path $snapshot 'tests/NativeHookUpdate/NativeHookUpdate.csproj') -c Release --runtime win-x64 --artifacts-path $artifacts $mapping -p:RestoreLockedMode=true 2>&1 |
    Tee-Object -FilePath (Join-Path $output 'tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Maintenance tests failed; no publish.' }
foreach ($suite in @('WorkbenchMaintenance','WorkbenchFocus')) {
    & $Dotnet run --project (Join-Path $snapshot "tests/$suite/$suite.csproj") -c Release --runtime win-x64 --artifacts-path $artifacts $mapping -p:RestoreLockedMode=true 2>&1 |
        Tee-Object -FilePath (Join-Path $output "$suite-tests.txt")
    if ($LASTEXITCODE -ne 0) { throw "$suite regression checks failed; no publish." }
}
& $Dotnet publish (Join-Path $snapshot 'src/ReforgerMcp/ReforgerMcp.csproj') -c Release -r win-x64 --self-contained false --artifacts-path $artifacts -o $server $mapping -p:ContinuousIntegrationBuild=true -p:RestoreLockedMode=true 2>&1 |
    Tee-Object -FilePath (Join-Path $output 'build.txt')
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
foreach ($source in $sources) {
    if ((Get-FileHash -LiteralPath (Join-Path $snapshot $source.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.sha256) {
        throw "Frozen build input changed: $($source.path). No discovery or deployment performed."
    }
}
& (Join-Path $server 'ReforgerMcp.exe') --config ([IO.Path]::GetFullPath($Configuration)) --hook-update (Join-Path $output 'request.json') 2>&1 |
    Tee-Object -FilePath (Join-Path $output 'discovery.txt')
if ($LASTEXITCODE -ne 0) { throw 'Discovery failed; inspect retained evidence. Nothing deployed.' }
$hashes = @(Get-ChildItem -LiteralPath $server -File -Recurse | Sort-Object FullName | ForEach-Object {
    @{ path = [IO.Path]::GetRelativePath($server, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'server-hashes.json') -Encoding utf8
Write-Output "Built server and hook evidence: $output"
Write-Output 'Discovery candidates require ABI and live validation. Existing runtime hash guards are unchanged; nothing deployed.'

