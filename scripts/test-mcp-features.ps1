param(
    [Parameter(Mandatory)][string]$Configuration,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ExpectedServerDllSha256
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new evidence directory.' }
$build = Join-Path $root ('work/acceptance-' + [Guid]::NewGuid().ToString('N'))
& dotnet run --project (Join-Path $root 'tests/McpAcceptance') -c Release --artifacts-path $build -- --self-test
if ($LASTEXITCODE -ne 0) { throw 'Acceptance gate regression checks failed.' }
$config = Get-Content -LiteralPath $Configuration -Raw | ConvertFrom-Json
function Get-ListenerIdentity {
    try {
        $owners = @(Get-NetTCPConnection -LocalPort $config.localHttp.port -State Listen | Select-Object -ExpandProperty OwningProcess -Unique)
        if ($owners.Count -ne 1) { throw 'Cannot uniquely identify the MCP listener.' }
        $process = Get-Process -Id $owners[0]
        $dll = Join-Path (Split-Path $process.Path -Parent) 'ReforgerMcp.dll'
        @{ processId = $process.Id; executable = $process.Path; startTime = $process.StartTime.ToUniversalTime().ToString('o');
            serverDll = $dll; sha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant() }
    } catch { @{ error = $_.Exception.Message } }
}
$before = Get-ListenerIdentity
& dotnet run --project (Join-Path $root 'tests/McpAcceptance') -c Release --artifacts-path $build --no-restore -- ([IO.Path]::GetFullPath($Configuration)) $output
$code = $LASTEXITCODE
$reportPath = Join-Path $output 'report.json'
if (!(Test-Path -LiteralPath $reportPath)) { throw 'Acceptance runner failed without a report.' }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$after = Get-ListenerIdentity
$stable = !$before.error -and !$after.error -and $before.processId -eq $after.processId -and $before.startTime -eq $after.startTime -and $before.sha256 -eq $after.sha256 -and $before.executable -eq $after.executable
$identity = @{ before = $before; after = $after; stable = $stable; expectedSha256 = $ExpectedServerDllSha256;
    matchesExpected = [bool]($stable -and $ExpectedServerDllSha256 -and $after.sha256 -eq $ExpectedServerDllSha256);
    limitation = 'Same listener process and adjacent on-disk DLL observed before and after; not a measurement of loaded memory.' }
$identity | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'server-identity.json') -Encoding utf8
if (!$identity.matchesExpected) {
    $report.updateSucceeded = $false
    if ($code -eq 0) { $code = 2 }
    $report.exitCode = $code
}
$report | Add-Member -NotePropertyName deploymentIdentityVerified -NotePropertyValue ([bool]$identity.matchesExpected)
$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $reportPath -Encoding utf8
$inputs = @('tests/McpAcceptance/Program.cs', 'tests/McpAcceptance/McpAcceptance.csproj', 'scripts/test-mcp-features.ps1', 'src/ReforgerMcp/packages.lock.json')
@($inputs | ForEach-Object { @{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $root $_) -Algorithm SHA256).Hash.ToLowerInvariant() } }) |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'test-source-hashes.json') -Encoding utf8
$rows = $report.tools | ForEach-Object {
    '<tr><td>' + [Net.WebUtility]::HtmlEncode($_.tool) + '</td><td>' + $_.status + '</td><td>' + [Net.WebUtility]::HtmlEncode($_.evidence) + '</td></tr>'
}
$html = '<!doctype html><meta charset="utf-8"><title>MCP acceptance</title><style>body{font:16px system-ui;max-width:1200px;margin:40px auto;padding:20px}td,th{text-align:left;padding:10px;border-bottom:1px solid #ddd}h1{color:#933}</style>' +
    '<h1>Update acceptance: ' + $(if ($report.updateSucceeded) {'PASSED'} else {'NOT CONFIRMED'}) +
    '</h1><p>' + [Net.WebUtility]::HtmlEncode($report.scope) + '</p><p>Exit code: ' + $code +
    '. Deployment identity verified: ' + $report.deploymentIdentityVerified +
    '</p><table><tr><th>Tool</th><th>Result</th><th>Evidence / limitation</th></tr>' + ($rows -join '') + '</table>'
$html | Set-Content -LiteralPath (Join-Path $output 'report.html') -Encoding utf8
Write-Output "Acceptance evidence: $output"
exit $code

