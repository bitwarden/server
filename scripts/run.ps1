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

$letsEncryptLivePath = "${outputDir}/letsencrypt/live"
if(Test-Path -Path $letsEncryptLivePath) {
    docker run -it --rm --name certbot -p 443:443 -p 80:80 -v $outputDir/letsencrypt:/etc/letsencrypt/ certbot/certbot `
        renew --logs-dir $outputDir/letsencrypt/logs --staging
}

docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.macwin.yml down
docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.macwin.yml up -d
