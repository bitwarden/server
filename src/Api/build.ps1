$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building API"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Clean"
dotnet clean $dir\Api.csproj -f netcoreapp2.1 -c "Release" -o $dir\obj\Docker\publish\Api
dotnet clean $dir\..\Jobs\Jobs.csproj -f netcoreapp2.1 -c "Release" -o $dir\obj\Docker\publish\Jobs
echo "Publish"
dotnet publish $dir\Api.csproj -f netcoreapp2.1 -c "Release" -o $dir\obj\Docker\publish\Api
dotnet publish $dir\..\Jobs\Jobs.csproj -f netcoreapp2.1 -c "Release" -o $dir\obj\Docker\publish\Jobs

echo "`nBuilding docker image"
docker --version
docker build -t bitwarden/api $dir\.
