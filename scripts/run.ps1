param (
    [string]$outputDir = "../.",
    [string]$coreVersion = "latest",
    [string]$webVersion = "latest",
    [switch] $install,
    [switch] $start,
    [switch] $restart,
    [switch] $stop,
    [switch] $pull,
    [switch] $updatedb,
    [switch] $update
)

# Setup

$dockerDir = "${outputDir}\docker"

# Functions

function Install() {
    [string]$letsEncrypt = "n"
    Write-Host "(!) " -f cyan -nonewline
    [string]$domain = $( Read-Host "Enter the domain name for your Bitwarden instance (ex. bitwarden.company.com)" )
    echo ""
    
    if ($domain -eq "") {
        $domain = "localhost"
    }
    
    if ($domain -ne "localhost") {
        Write-Host "(!) " -f cyan -nonewline
        $letsEncrypt = $( Read-Host "Do you want to use Let's Encrypt to generate a free SSL certificate? (y/n)" )
        echo ""
    
        if ($letsEncrypt -eq "y") {
            Write-Host "(!) " -f cyan -nonewline
            [string]$email = $( Read-Host ("Enter your email address (Let's Encrypt will send you certificate " +
                "expiration reminders)") )
            echo ""
    
            $letsEncryptPath = "${outputDir}/letsencrypt"
            if (!(Test-Path -Path $letsEncryptPath )) {
                New-Item -ItemType directory -Path $letsEncryptPath | Out-Null
            }
            docker pull certbot/certbot
            docker run -it --rm --name certbot -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
                certonly --standalone --noninteractive --agree-tos --preferred-challenges http `
                --email $email -d $domain --logs-dir /etc/letsencrypt/logs
        }
    }
    
    Pull-Setup
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -install 1 -domain ${domain} -letsencrypt ${letsEncrypt} `
        -os win -corev $coreVersion -webv $webVersion
}

function Docker-Compose-Up {
    Docker-Compose-Files
    docker-compose up -d
}

function Docker-Compose-Down {
    Docker-Compose-Files
    docker-compose down
}

function Docker-Compose-Pull {
    Docker-Compose-Files
    docker-compose pull
}

function Docker-Compose-Files {
    if (Test-Path -Path "${dockerDir}\docker-compose.override.yml" -PathType leaf) {
        $env:COMPOSE_FILE = "${dockerDir}\docker-compose.yml;${dockerDir}\docker-compose.override.yml"
    }
    else {
        $env:COMPOSE_FILE = "${dockerDir}\docker-compose.yml"
    }
    $env:COMPOSE_HTTP_TIMEOUT = "300"
}

function Docker-Prune {
    docker image prune --all --force --filter="label=com.bitwarden.product=bitwarden" `
        --filter="label!=com.bitwarden.project=setup"
}

function Update-Lets-Encrypt {
    if (Test-Path -Path "${outputDir}\letsencrypt\live") {
        docker pull certbot/certbot
        docker run -it --rm --name certbot -p 443:443 -p 80:80 `
            -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
            renew --logs-dir /etc/letsencrypt/logs
    }
}

function Update-Database {
    Pull-Setup
    docker run -it --rm --name setup --network container:bitwarden-mssql `
        -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -update 1 -db 1 -os win -corev $coreVersion -webv $webVersion
    echo "Database update complete"
}

function Update([switch] $withpull) {
    if ($withpull) {
        Pull-Setup
    }
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

if ($install) {
    Install
}
elseif ($start -Or $restart) {
    Restart
}
elseif ($pull) {
    Docker-Compose-Pull
}
elseif ($stop) {
    Docker-Compose-Down
}
elseif ($updatedb) {
    Update-Database
}
elseif ($update) {
    Docker-Compose-Down
    Update -withpull
    Restart
    echo "Pausing 60 seconds for database to come online. Please wait..."
    Start-Sleep -s 60
    Update-Database
}
elseif ($rebuild) {
    Docker-Compose-Down
    Update
}
