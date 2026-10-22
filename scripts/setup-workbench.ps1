param(
    [Parameter(Mandatory)][string]$Config,
    [ValidateSet('status', 'install', 'enable')][string]$Action = 'install',
    [string]$Server = (Join-Path (Split-Path $PSScriptRoot -Parent) 'ReforgerMcp.exe')
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Server -PathType Leaf)) { throw "Server executable not found: $Server" }
if (!(Test-Path -LiteralPath $Config -PathType Leaf)) { throw "Configuration not found: $Config" }
& $Server --config ([IO.Path]::GetFullPath($Config)) --setup-runtime $Action
if ($LASTEXITCODE -ne 0) { throw 'Runtime setup is not ready. Inspect the reported reason; do not blindly retry an uncertain operation.' }
