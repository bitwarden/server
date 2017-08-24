$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building nginx"

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/nginx $dir\.
