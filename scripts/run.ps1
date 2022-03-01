$scriptPath = $MyInvocation.MyCommand.Path
$bitwardenPath = Split-Path $scriptPath | Split-Path | Split-Path
$files = Get-ChildItem $bitwardenPath
$scriptFound = $false
foreach ($file in $files) {
    if ($file.Name -eq "bitwarden.ps1") {
        $scriptFound = $true
        Invoke-RestMethod -OutFile "$($bitwardenPath)/bitwarden.ps1" -Uri "https://go.btwrdn.co/bw-ps"
        Write-Output "We have updated the location of our scripts, please run 'bitwarden.ps1' again."
        break
    }
}

if (-not $scriptFound) {
    Write-Output "We have updated our script locations, please run 'bitwarden.ps1 -updateself' before updating."
}
