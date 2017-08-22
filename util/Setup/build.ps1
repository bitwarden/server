$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n# Building Setup"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
dotnet clean $dir\Setup.csproj -f netcoreapp2.0 -c "Release" -o $dir\obj\Docker\publish
dotnet publish $dir\Setup.csproj -f netcoreapp2.0 -c "Release" -o $dir\obj\Docker\publish

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/setup $dir\.
