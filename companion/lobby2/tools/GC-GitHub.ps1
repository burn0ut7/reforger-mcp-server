param([ValidateSet('Check','SignIn')][string]$Action='Check')
# Authentication belongs to Git Credential Manager. Only safe status leaves this script.
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
function Get-GcGitHubAuth {
    $oldTrace=$env:GCM_TRACE_SECRETS;$env:GCM_TRACE_SECRETS='0'
    $oldInteractive=$env:GCM_INTERACTIVE;$oldStore=$env:GCM_CREDENTIAL_STORE;$oldPrompt=$env:GIT_TERMINAL_PROMPT
    try {
        $env:GCM_INTERACTIVE='Never';$env:GCM_CREDENTIAL_STORE='wincredman';$env:GIT_TERMINAL_PROMPT='0'
        $credential=@("protocol=https`nhost=github.com`n`n" | & git.exe credential-manager get 2>$null)
        if ($LASTEXITCODE -ne 0) {throw 'Sign in on GitHub to continue.'}
        $secret=$credential | Where-Object {$_ -clike 'password=*'} | Select-Object -First 1
        if (!$secret) {throw 'Sign in on GitHub to continue.'}
        @{Authorization='Bearer '+$secret.Substring(9);Accept='application/vnd.github+json';'User-Agent'='GC-Workbench'}
    } finally {$credential=$null;$secret=$null;$env:GCM_TRACE_SECRETS=$oldTrace;$env:GCM_INTERACTIVE=$oldInteractive;$env:GCM_CREDENTIAL_STORE=$oldStore;$env:GIT_TERMINAL_PROMPT=$oldPrompt}
}
function Get-GcGitHubStatus {
    if (!(Get-Command git.exe -ErrorAction SilentlyContinue)) {return @{Status='Install Git for Windows';State='warning';Connected=$false;Repository='Not checked'}}
    try {$auth=Get-GcGitHubAuth} catch {return @{Status='Sign in required';State='warning';Connected=$false;Repository='Not checked'}}
    try {
        $user=Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $auth -TimeoutSec 10 -ErrorAction Stop
        if (!$user.login) {throw 'Unknown account'}
    } catch {
        $code=0;if ($_.Exception.Response) {$code=[int]$_.Exception.Response.StatusCode}
        if ($code -eq 401) {return @{Status='Sign-in expired';State='warning';Connected=$false;Repository='Not checked'}}
        return @{Status='Connection could not be verified';State='warning';Connected=$false;Repository='Not checked'}
    }
    $access='Accessible'
    try {$null=Invoke-RestMethod -Uri 'https://api.github.com/repos/Global-Conflicts-ArmA/gc-reforger-missions' -Headers $auth -TimeoutSec 10 -ErrorAction Stop}
    catch {
        $code=0;if ($_.Exception.Response) {$code=[int]$_.Exception.Response.StatusCode}
        $access=if($code -in 403,404){'Access unavailable'}else{'Could not check access'}
    }
    @{Status=('Connected as '+$user.login);State='ready';Connected=$true;Repository=$access}
}
function Connect-GcGitHub {
    $oldTrace=$env:GCM_TRACE_SECRETS;$env:GCM_TRACE_SECRETS='0'
    $oldInteractive=$env:GCM_INTERACTIVE;$oldStore=$env:GCM_CREDENTIAL_STORE
    try {
        $env:GCM_INTERACTIVE='true';$env:GCM_CREDENTIAL_STORE='wincredman'
        $null=& git.exe credential-manager github login --browser --force 2>&1
        if ($LASTEXITCODE -ne 0) {return @{Status='Sign-in cancelled or unavailable';State='warning';Connected=$false;Repository='Not checked'}}
        Get-GcGitHubStatus
    } catch {return @{Status='Install Git with Git Credential Manager';State='warning';Connected=$false;Repository='Not checked'}}
    finally {$env:GCM_TRACE_SECRETS=$oldTrace;$env:GCM_INTERACTIVE=$oldInteractive;$env:GCM_CREDENTIAL_STORE=$oldStore}
}
if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference='Continue'
    $result=if($Action -eq 'SignIn'){Connect-GcGitHub}else{Get-GcGitHubStatus}
    $result | ConvertTo-Json -Compress
}
