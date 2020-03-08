$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Identity"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\Identity.csproj
echo "Clean"
dotnet clean $dir\Identity.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Publish"
dotnet publish $dir\Identity.csproj -c "Release" -o $dir\obj\Azure\publish
