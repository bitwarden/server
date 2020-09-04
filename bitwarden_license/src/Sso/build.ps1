$curDir = pwd
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Sso"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\Sso.csproj
echo "Clean"
dotnet clean $dir\Sso.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Node Build"
cd $dir
npm install
cd $curDir
gulp --gulpfile $dir\gulpfile.js build
echo "Publish"
dotnet publish $dir\Sso.csproj -c "Release" -o $dir\obj\Azure\publish
