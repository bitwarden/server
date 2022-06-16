$scriptPath = $MyInvocation.MyCommand.Path
Invoke-RestMethod -OutFile $scriptPath -Uri "https://go.btwrdn.co/bw-ps"
Write-Output "We have moved our self-hosted scripts to their own repository (https://github.com/bitwarden/self-host).  Your 'bitwarden.ps1' script has been automatically upgraded. Please run it again."
