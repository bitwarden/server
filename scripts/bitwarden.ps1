$scriptPath = $MyInvocation.MyCommand.Path
Invoke-RestMethod -OutFile $scriptPath -Uri "https://go.btwrdn.co/bw-ps"
Write-Output "We have updated the location of our scripts, please run 'bitwarden.ps1' again."
