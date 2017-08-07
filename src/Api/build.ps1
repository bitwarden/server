$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n# Building API"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
dotnet publish $dir\Api.csproj -f netcoreapp2.0 -c "Release" -o $dir\obj\Docker\publish

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/api $dir\.
