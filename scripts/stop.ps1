param (
    [string] $dockerDir = ""
)

$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
if($dockerDir -eq "") {
    $dockerDir="${dir}\..\docker"
}

docker --version
docker-compose --version

docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.macwin.yml down
