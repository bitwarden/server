$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Scim"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\Scim.csproj
echo "Clean"
dotnet clean $dir\Scim.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Publish"
dotnet publish $dir\Scim.csproj -c "Release" -o $dir\obj\Azure\publish
