param (
    [string]$outputDir = "../.",
    [string]$dockerDir = "",
    [switch] $start,
    [switch] $restart,
    [switch] $stop,
    [switch] $pull,
    [switch] $updatedb
)

# Setup

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if($dockerDir -eq "") {
    $dockerDir="${dir}\..\docker"
}

# Functions

function Docker-Compose-Up {
    docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.macwin.yml up -d
}

function Docker-Compose-Down {
    docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.macwin.yml down
}

function Docker-Compose-Pull {
    docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.macwin.yml pull
}

function Docker-Prune {
    docker image prune -f
}

function Update-Lets-Encrypt {
    if(Test-Path -Path "${outputDir}\letsencrypt\live") {
        docker run -it --rm --name certbot -p 443:443 -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
            renew --logs-dir /etc/letsencrypt/logs
    }
}

function Update-Database {
    docker run -it --rm --name setup --network container:mssql -v ${outputDir}:/bitwarden bitwarden/setup `
        dotnet Setup.dll -update 1 -db 1
    echo "Database update complete"
}

function Print-Environment {
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup `
        dotnet Setup.dll -printenv 1 -env win
}

# Commands

if($start -Or $restart) {
    Docker-Compose-Down
    Docker-Compose-Pull
    Update-Lets-Encrypt
    Docker-Compose-Up
    Docker-Prune
    Print-Environment
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
