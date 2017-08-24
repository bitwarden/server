$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Nginx"

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/nginx $dir\.
