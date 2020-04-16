$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building API"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\Api.csproj
echo "Clean"
dotnet clean $dir\Api.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Publish"
dotnet publish $dir\Api.csproj -c "Release" -o $dir\obj\Azure\publish
