$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n# Building Server"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
dotnet publish $dir\Server.csproj -c "Release" -o $dir\obj\Docker\publish

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/server $dir\.
