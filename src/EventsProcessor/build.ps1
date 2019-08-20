$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Events Processor"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\EventsProcessor.csproj
echo "Clean"
dotnet clean $dir\EventsProcessor.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Publish"
dotnet publish $dir\EventsProcessor.csproj -c "Release" -o $dir\obj\Azure\publish
