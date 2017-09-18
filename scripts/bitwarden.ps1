param (
    [switch] $install,
    [switch] $start,
    [switch] $restart,
    [switch] $stop,
    [switch] $update,
    [switch] $updatedb,
    [switch] $updateself,
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

===================================================
"

docker --version
docker-compose --version

echo ""

# Setup

$scriptPath = $MyInvocation.MyCommand.Path
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if($output -eq "") {
    $output="${dir}\bitwarden"
}

$scriptsDir = "${output}\scripts"
$dockerDir = "${output}\docker"
$githubBaseUrl = "https://raw.githubusercontent.com/bitwarden/core/master"

# Functions

function Download-Self {
    if(!(Test-Path -Path $scriptsDir)) {
        New-Item -ItemType directory -Path $scriptsDir | Out-Null
    }
    Invoke-RestMethod -OutFile $scriptPath -Uri "${githubBaseUrl}/scripts/bitwarden.ps1"
}

function Download-Install {
    if(!(Test-Path -Path $scriptsDir)) {
        New-Item -ItemType directory -Path $scriptsDir | Out-Null
    }
    Invoke-RestMethod -OutFile $scriptsDir\install.ps1 ` -Uri "${githubBaseUrl}/scripts/install.ps1"
}

function Download-Run-File {
    if(!(Test-Path -Path $scriptsDir)) {
        New-Item -ItemType directory -Path $scriptsDir | Out-Null
    }
    Invoke-RestMethod -OutFile $scriptsDir\run.ps1 -Uri "${githubBaseUrl}/scripts/run.ps1"
}

function Download-Docker-Files {
    Invoke-RestMethod -OutFile $dockerDir\docker-compose.yml -Uri "${githubBaseUrl}/docker/docker-compose.yml"
    Invoke-RestMethod -OutFile $dockerDir\docker-compose.macwin.yml ` -Uri "${githubBaseUrl}/docker/docker-compose.macwin.yml"
    Invoke-RestMethod -OutFile $dockerDir\global.env -Uri "${githubBaseUrl}/docker/global.env"
    Invoke-RestMethod -OutFile $dockerDir\mssql.env -Uri "${githubBaseUrl}/docker/mssql.env"
}

function Check-Output-Dir-Exists {
    if(!(Test-Path -Path $output)) {
        throw "Cannot find a bitwarden installation at $output."
    }
}

function Check-Output-Dir-Not-Exists {
    if(Test-Path -Path $output) {
        throw "Looks like bitwarden is already installed at $output."
    }
}

# Commands

if($install) {
    Check-Output-Dir-Not-Exists
    New-Item -ItemType directory -Path $output | Out-Null
    Download-Install
    Invoke-Expression "$scriptsDir\install.ps1 -outputDir $output"
}
elseif($start -Or $restart) {
    Check-Output-Dir-Exists
    if(!(Test-Path -Path $dockerDir)) {
        New-Item -ItemType directory -Path $dockerDir | Out-Null
        Download-All-Files
    }

    Invoke-Expression "$scriptsDir\run.ps1 -restart -outputDir $output -dockerDir $dockerDir"
}
elseif($update) {
    Check-Output-Dir-Exists
    if(Test-Path -Path $dockerDir) {
        Remove-Item -Recurse -Force $dockerDir | Out-Null
    }
    New-Item -ItemType directory -Path $dockerDir | Out-Null

    Download-All-Files
    Invoke-Expression "$scriptsDir\run.ps1 -restart -outputDir $output -dockerDir $dockerDir"
    Invoke-Expression "$scriptsDir\run.ps1 -updatedb -outputDir $output -dockerDir $dockerDir"
}
elseif($updatedb) {
    Check-Output-Dir-Exists
    Invoke-Expression "$scriptsDir\run.ps1 -updatedb -outputDir $output -dockerDir $dockerDir"
}
elseif($stop) {
    Check-Output-Dir-Exists
    Invoke-Expression "$scriptsDir\run.ps1 -stop -outputDir $output -dockerDir $dockerDir"
}
elseif($updateself) {
    Check-Output-Dir-Exists
    Download-Self
    echo "Updated self."
}
else {
    echo "No command found."
}
