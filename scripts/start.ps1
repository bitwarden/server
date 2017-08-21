param (
    [string]$outputDir = "../.",
    [string]$dockerDir = ""
)

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if($dockerDir -eq "") {
    $dockerDir="${dir}\..\docker"
}

docker --version
docker-compose --version

docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.macwin.yml down

$letsEncryptPath = "${outputDir}/letsencrypt"
if(Test-Path -Path $letsEncryptPath) {
    docker run -it --rm --name certbot -p 443:443 -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
        renew --logs-dir /etc/letsencrypt/logs
}

docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.macwin.yml up -d
docker image prune -f
