$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../GC-GitHub.ps1')
function Assert($condition,$message){if(!$condition){throw $message}}
function Get-GcGitHubAuth {if($script:missing){throw 'no credential'};@{Authorization='Bearer test-only'}}
$script:mode='ready';$script:missing=$false
function Invoke-RestMethod {
    param($Uri,$Headers,$TimeoutSec,$ErrorAction)
    if($script:mode -eq 'offline'){throw 'offline'}
    if($script:mode -eq 'expired' -or ($script:mode -eq 'denied' -and $Uri -like '*repos/*')){
        $error=New-Object Exception 'test response'
        $error | Add-Member NoteProperty Response @{StatusCode=$(if($script:mode -eq 'expired'){401}else{403})}
        throw $error
    }
    if($Uri -eq 'https://api.github.com/user'){return @{login='test-user'}}
    return @{name='missions'}
}
$script:missing=$true;Assert (!(Get-GcGitHubStatus).Connected) 'Missing credential accepted'
$script:missing=$false
Assert ((Get-GcGitHubStatus).Status -eq 'Connected as test-user') 'Verified identity missing'
$script:mode='expired';Assert ((Get-GcGitHubStatus).Status -eq 'Sign-in expired') 'Expired token accepted'
$script:mode='offline';Assert ((Get-GcGitHubStatus).Status -eq 'Connection could not be verified') 'Offline treated as connected'
$script:mode='denied';Assert ((Get-GcGitHubStatus).Repository -eq 'Access unavailable') 'Repository access not checked'
$script:mode='ready'
function git.exe {
    Assert (($args -join ' ') -eq 'credential-manager github login --browser --force') 'Nonbrowser login'
    Assert ($env:GCM_CREDENTIAL_STORE -eq 'wincredman') 'Unsafe credential store'
    $global:LASTEXITCODE=0
}
Assert ((Connect-GcGitHub).Connected) 'Post-login connection not verified'
'PASS: missing/expired/offline/connected account, repository access, browser-only sign-in, Windows credential store.'
. (Join-Path $PSScriptRoot '../GC-ModWorkflow.ps1')
$originalStore=$env:GCM_CREDENTIAL_STORE;$originalTrace=$env:GCM_TRACE_SECRETS
try {
    $env:GCM_CREDENTIAL_STORE='plaintext';$env:GCM_TRACE_SECRETS='1'
    function git.exe {
        Assert ($env:GCM_CREDENTIAL_STORE -eq 'wincredman' -and $env:GCM_TRACE_SECRETS -eq '0') 'Git inherited unsafe credential settings'
        $global:LASTEXITCODE=0
        'safe result'
    }
    $null=Git 'unused-fixture-path' @('--version')
    Assert ($env:GCM_CREDENTIAL_STORE -eq 'plaintext' -and $env:GCM_TRACE_SECRETS -eq '1') 'Caller environment not restored'
} finally {$env:GCM_CREDENTIAL_STORE=$originalStore;$env:GCM_TRACE_SECRETS=$originalTrace}
'PASS: Git operations force secure storage without changing caller settings.'
