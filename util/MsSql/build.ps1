$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n# Building MsSql"

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/mssql $dir\.
