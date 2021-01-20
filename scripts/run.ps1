param (
    [string]$outputDir = "../.",
    [string]$coreVersion = "latest",
    [string]$webVersion = "latest",
    [switch] $install,
    [switch] $start,
    [switch] $restart,
    [switch] $stop,
    [switch] $pull,
    [switch] $updateconf,
    [switch] $renewcert,
    [switch] $updatedb,
    [switch] $update
)

# Setup

$dockerDir = "${outputDir}\docker"
$setupQuiet = 0
$qFlag = ""
$quietPullFlag = ""
$certbotHttpPort = "80"
$certbotHttpsPort = "443"
if($env:BITWARDEN_QUIET -eq "true") {
    $setupQuiet = 1
    $qFlag = " -q"
    $quietPullFlag = " --quiet-pull"
}
if("${env:BITWARDEN_CERTBOT_HTTP_PORT}" -ne "") {
    $certbotHttpPort = $env:BITWARDEN_CERTBOT_HTTP_PORT
}
if("${env:BITWARDEN_CERTBOT_HTTPS_PORT}" -ne "") {
    $certbotHttpsPort = $env:BITWARDEN_CERTBOT_HTTPS_PORT
}

# Functions

function Install() {
    [string]$letsEncrypt = "n"
    Write-Host "(!) " -f cyan -nonewline
    [string]$domain = $( Read-Host "Enter the domain name for your Bitwarden instance (ex. bitwarden.example.com)" )
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
            Invoke-Expression ("docker pull{0} certbot/certbot" -f "") #TODO: qFlag
            $certbotExp = "docker run -it --rm --name certbot -p ${certbotHttpsPort}:443 -p ${certbotHttpPort}:80 " +`
                "-v ${outputDir}/letsencrypt:/etc/letsencrypt/ certbot/certbot " +`
                "certonly{0} --standalone --noninteractive --agree-tos --preferred-challenges http " +`
                "--email ${email} -d ${domain} --logs-dir /etc/letsencrypt/logs"
            Invoke-Expression ($certbotExp -f $qFlag)
        }
    }
    
    Pull-Setup
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -install 1 -domain ${domain} -letsencrypt ${letsEncrypt} `
        -os win -corev $coreVersion -webv $webVersion -q $setupQuiet
}

function Docker-Compose-Up {
    Docker-Compose-Files
    Docker-Compose-Volumes
    Invoke-Expression ("docker-compose up -d{0}" -f $quietPullFlag)
}

function Docker-Compose-Down {
    Docker-Compose-Files
    Invoke-Expression ("docker-compose down{0}" -f "") #TODO: qFlag
}

function Docker-Compose-Pull {
    Docker-Compose-Files
    Invoke-Expression ("docker-compose pull{0}" -f $qFlag)
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

function Docker-Compose-Volumes {
    Create-Dir "core"
    Create-Dir "core/attachments"
    Create-Dir "logs"
    Create-Dir "logs/admin"
    Create-Dir "logs/api"
    Create-Dir "logs/events"
    Create-Dir "logs/icons"
    Create-Dir "logs/identity"
    Create-Dir "logs/mssql"
    Create-Dir "logs/nginx"
    Create-Dir "logs/notifications"
    Create-Dir "logs/sso"
    Create-Dir "logs/portal"
    Create-Dir "mssql/backups"
    Create-Dir "mssql/data"
}

function Create-Dir($str) {
    $outPath = "${outputDir}/$str"
    if (!(Test-Path -Path $outPath )) {
        Write-Line "Creating directory $outPath"
        New-Item -ItemType directory -Path $outPath | Out-Null
    }
}

function Docker-Prune {
    docker image prune --all --force --filter="label=com.bitwarden.product=bitwarden" `
        --filter="label!=com.bitwarden.project=setup"
}

function Update-Lets-Encrypt {
    if (Test-Path -Path "${outputDir}\letsencrypt\live") {
        Invoke-Expression ("docker pull{0} certbot/certbot" -f "") #TODO: qFlag
        $certbotExp = "docker run -it --rm --name certbot -p ${certbotHttpsPort}:443 -p ${certbotHttpPort}:80 " +`
            "-v ${outputDir}/letsencrypt:/etc/letsencrypt/ certbot/certbot " +`
            "renew{0} --logs-dir /etc/letsencrypt/logs" -f $qFlag
        Invoke-Expression $certbotExp
    }
}

function Force-Update-Lets-Encrypt {
    if (Test-Path -Path "${outputDir}\letsencrypt\live") {
        Invoke-Expression ("docker pull{0} certbot/certbot" -f "") #TODO: qFlag
        $certbotExp = "docker run -it --rm --name certbot -p ${certbotHttpsPort}:443 -p ${certbotHttpPort}:80 " +`
            "-v ${outputDir}/letsencrypt:/etc/letsencrypt/ certbot/certbot " +`
            "renew{0} --logs-dir /etc/letsencrypt/logs --force-renew" -f $qFlag
        Invoke-Expression $certbotExp
    }
}

function Update-Database {
    Pull-Setup
    Docker-Compose-Files
    $mssqlId = docker-compose ps -q mssql
    docker run -it --rm --name setup --network container:$mssqlId `
        -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -update 1 -db 1 -os win -corev $coreVersion -webv $webVersion -q $setupQuiet
    Write-Line "Database update complete"
}

function Update([switch] $withpull) {
    if ($withpull) {
        Pull-Setup
    }
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -update 1 -os win -corev $coreVersion -webv $webVersion -q $setupQuiet
}

function Print-Environment {
    Pull-Setup
    docker run -it --rm --name setup -v ${outputDir}:/bitwarden bitwarden/setup:$coreVersion `
        dotnet Setup.dll -printenv 1 -os win -corev $coreVersion -webv $webVersion -q $setupQuiet
}

function Restart {
    Docker-Compose-Down
    Docker-Compose-Pull
    Update-Lets-Encrypt
    Docker-Compose-Up
    Print-Environment
}

function Cert-Restart {
    Docker-Compose-Down
    Docker-Compose-Pull
    Force-Update-Lets-Encrypt
    Docker-Compose-Up
    Print-Environment
}


function Pull-Setup {
    Invoke-Expression ("docker pull{0} bitwarden/setup:${coreVersion}" -f "") #TODO: qFlag
}

function Write-Line($str) {
    if($env:BITWARDEN_QUIET -ne "true") {
        Write-Host $str
    }
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
elseif ($renewcert) {
    Cert-Restart
}
elseif ($updateconf) {
    Docker-Compose-Down
    Update -withpull
}
elseif ($updatedb) {
    Update-Database
}
elseif ($update) {
    Docker-Compose-Down
    Update -withpull
    Restart
    Docker-Prune
    Write-Line "Pausing 60 seconds for database to come online. Please wait..."
    Start-Sleep -s 60
    Update-Database
}
elseif ($rebuild) {
    Docker-Compose-Down
    Update
}
