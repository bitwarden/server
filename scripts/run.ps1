$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dockerDir="${dir}\..\docker"

docker --version
docker-compose --version

docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.windows.yml down
docker-compose -f ${dockerDir}\docker-compose.yml -f ${dockerDir}\docker-compose.windows.yml up -d
