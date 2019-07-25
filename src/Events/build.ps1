$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Events"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\Events.csproj
echo "Clean"
dotnet clean $dir\Events.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Publish"
dotnet publish $dir\Events.csproj -c "Release" -o $dir\obj\Azure\publish
