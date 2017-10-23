$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Icons"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Clean"
dotnet clean $dir\Icons.csproj -f netcoreapp2.0 -c "Release" -o $dir\obj\Docker\publish
echo "Publish"
dotnet publish $dir\Icons.csproj -f netcoreapp2.0 -c "Release" -o $dir\obj\Docker\publish

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/icons $dir\.
