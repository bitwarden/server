$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n# Building Identity"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
dotnet publish $dir\Identity.csproj -f netcoreapp2.0 -c "Release" -o $dir\obj\Docker\publish

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/identity $dir\.
