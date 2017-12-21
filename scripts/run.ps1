param (
    [string]$outputDir = "../.",
    [string]$coreVersion = "latest",
    [string]$webVersion = "latest",
    [switch] $start,
    [switch] $restart,
    [switch] $stop,
    [switch] $pull,
    [switch] $updatedb,
    [switch] $update
)

# Setup

$dockerDir="${outputDir}\docker"

# Functions

function Docker-Compose-Up {
    docker-compose -f ${dockerDir}\docker-compose.yml up -d
}

function Docker-Compose-Down {
    docker-compose -f ${dockerDir}\docker-compose.yml down
}

function Docker-Compose-Pull {
    docker-compose -f ${dockerDir}\docker-compose.yml pull
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
    docker run -it --rm --name setup --network container:bitwarden-mssql -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -update 1 -db 1 -os win -corev $coreVersion -webv $webVersion
    echo "Database update complete"
}

function Update {
    Pull-Setup
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -update 1 -os win -corev $coreVersion -webv $webVersion
}

function Print-Environment {
    Pull-Setup
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -printenv 1 -os win -corev $coreVersion -webv $webVersion
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
    docker pull bitwarden/setup:$coreVersion
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
