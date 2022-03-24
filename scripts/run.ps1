$scriptPath = $MyInvocation.MyCommand.Path
$bitwardenPath = Split-Path $scriptPath | Split-Path | Split-Path
$files = Get-ChildItem $bitwardenPath
$scriptFound = $false
foreach ($file in $files) {
    if ($file.Name -eq "bitwarden.ps1") {
        $scriptFound = $true
        Invoke-RestMethod -OutFile "$($bitwardenPath)/bitwarden.ps1" -Uri "https://go.btwrdn.co/bw-ps"
        Write-Output "We have moved our self-hosted scripts to their own repository (https://github.com/bitwarden/self-host).  Your 'bitwarden.ps1' script has been automatically upgraded. Please run it again."
        break
    }
}

if (-not $scriptFound) {
    Write-Output "We have moved our self-hosted scripts to their own repository (https://github.com/bitwarden/self-host).  Please run 'bitwarden.ps1 -updateself' before updating."
}
