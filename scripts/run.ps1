param (
    [string]$outputDir = "../.",
    [string]$dockerDir = "",
    [switch] $start,
    [switch] $restart,
    [switch] $stop,
    [switch] $pull,
    [switch] $updatedb,
    [switch] $update
)

# Setup

[string]$tag = "1.13.1"

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if($dockerDir -eq "") {
    $dockerDir="${dir}\..\docker"
}

# Functions

function Docker-Compose-Up {
    docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.linwin.yml up -d
}

function Docker-Compose-Down {
    docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.linwin.yml down
}

function Docker-Compose-Pull {
    docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.linwin.yml pull
}

function Docker-Prune {
    docker image prune -f
}

function Update-Lets-Encrypt {
    if(Test-Path -Path "${outputDir}\letsencrypt\live") {
        docker pull certbot/certbot
        docker run -it --rm --name certbot -p 443:443 -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
            renew --logs-dir /etc/letsencrypt/logs
    }
}

function Update-Database {
    Pull-Setup
    docker run -it --rm --name setup --network container:mssql -v ${outputDir}:/bitwarden bitwarden/setup:$tag `
        dotnet Setup.dll -update 1 -db 1
    echo "Database update complete"
}

function Update {
    Pull-Setup
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$tag `
        dotnet Setup.dll -update 1
}

function Print-Environment {
    Pull-Setup
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$tag `
        dotnet Setup.dll -printenv 1 -env win
}

function Restart {
    Docker-Compose-Down
    Docker-Compose-Pull
    Update-Lets-Encrypt
    Docker-Compose-Up
    Docker-Prune
    Print-Environment
}

function Pull-Setup {
    docker pull bitwarden/setup:$tag
}

# Commands

if($start -Or $restart) {
    Restart
}
elseif($pull) {
    Docker-Compose-Pull
}
elseif($stop) {
    Docker-Compose-Down
}
elseif($updatedb) {
    Update-Database
}
elseif($update) {
    Docker-Compose-Down
    Update
    Restart
    Update-Database
}
