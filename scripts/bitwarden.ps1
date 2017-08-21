param (
    [switch] $install,
    [switch] $run,
    [switch] $restart,
    [switch] $stop,
    [switch] $update,
    [switch] $updatedb,
    [string] $output = ""
)

$year = (Get-Date).year

Write-Host @'
 _     _ _                         _            
| |__ (_) |___      ____ _ _ __ __| | ___ _ __  
| '_ \| | __\ \ /\ / / _` | '__/ _` |/ _ \ '_ \ 
| |_) | | |_ \ V  V / (_| | | | (_| |  __/ | | |
|_.__/|_|\__| \_/\_/ \__,_|_|  \__,_|\___|_| |_|
'@

Write-Host "
Open source password management solutions
Copyright 2015-${year}, 8bit Solutions LLC
https://bitwarden.com, https://github.com/bitwarden
"

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if($output -eq "") {
    $output="${dir}\bitwarden"
}

if(!(Test-Path -Path $output)) {
    New-Item -ItemType directory -Path $output | Out-Null
}

$scriptsDir = "${output}\scripts"
$dockerDir = "${output}\docker"
$githubBaseUrl = "https://raw.githubusercontent.com/bitwarden/core/master"

if(!(Test-Path -Path $scriptsDir)) {
    New-Item -ItemType directory -Path $scriptsDir | Out-Null
}

function Download-Run-Files {
    Invoke-RestMethod -OutFile $scriptsDir\run.ps1 -Uri "${githubBaseUrl}/scripts/run.ps1"
    Invoke-RestMethod -OutFile $scriptsDir\stop.ps1 -Uri "${githubBaseUrl}/scripts/stop.ps1"
    Invoke-RestMethod -OutFile $dockerDir\docker-compose.yml -Uri "${githubBaseUrl}/docker/docker-compose.yml"
    Invoke-RestMethod -OutFile $dockerDir\docker-compose.macwin.yml ` -Uri "${githubBaseUrl}/docker/docker-compose.macwin.yml"
    Invoke-RestMethod -OutFile $dockerDir\global.env -Uri "${githubBaseUrl}/docker/global.env"
    Invoke-RestMethod -OutFile $dockerDir\mssql.env -Uri "${githubBaseUrl}/docker/mssql.env"
}

if($install) {
    Invoke-RestMethod -OutFile $scriptsDir\install.ps1 ` -Uri "${githubBaseUrl}/scripts/install.ps1"
    Invoke-Expression "$scriptsDir\install.ps1 -outputDir $output"
}
elseif($run -Or $restart) {
    if(!(Test-Path -Path $dockerDir)) {
        New-Item -ItemType directory -Path $dockerDir | Out-Null
        Download-Run-Files
    }

    Invoke-Expression "$scriptsDir\run.ps1 -dockerDir $dockerDir"
}
elseif($update) {
    if(Test-Path -Path $dockerDir) {
        Remove-Item -Recurse -Force $dockerDir | Out-Null
    }
    New-Item -ItemType directory -Path $dockerDir | Out-Null

    Download-Run-Files
    Invoke-Expression "$scriptsDir\run.ps1 -dockerDir $dockerDir"
}
elseif($updatedb) {
    Invoke-RestMethod -OutFile $scriptsDir\update-db.ps1 -Uri "${githubBaseUrl}/scripts/update-db.ps1"
    Invoke-Expression "$scriptsDir\update-db.ps1 -outputDir $output"
}
elseif($stop) {
    Invoke-Expression "$scriptsDir\stop.ps1 -dockerDir $dockerDir"
}
else {
    echo "No command found."
}
