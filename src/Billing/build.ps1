$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Billing"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\Billing.csproj
echo "Clean"
dotnet clean $dir\Billing.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Publish"
dotnet publish $dir\Billing.csproj -c "Release" -o $dir\obj\Azure\publish
