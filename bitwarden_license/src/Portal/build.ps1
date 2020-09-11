$curDir = pwd
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Portal"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\Portal.csproj
echo "Clean"
dotnet clean $dir\Portal.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Node Build"
cd $dir
npm install
cd $curDir
gulp --gulpfile $dir\gulpfile.js build
echo "Publish"
dotnet publish $dir\Portal.csproj -c "Release" -o $dir\obj\Azure\publish
