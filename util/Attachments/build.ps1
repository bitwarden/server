$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Attachments"

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/attachments $dir\.
