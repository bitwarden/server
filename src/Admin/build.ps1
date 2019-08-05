$curDir = pwd
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

echo "`n## Building Admin"

echo "`nBuilding app"
echo ".NET Core version $(dotnet --version)"
echo "Restore"
dotnet restore $dir\Admin.csproj
echo "Clean"
dotnet clean $dir\Admin.csproj -c "Release" -o $dir\obj\Azure\publish
echo "Node Build"
cd $dir
npm install
cd $curDir
gulp --gulpfile $dir\gulpfile.js build
echo "Publish"
dotnet publish $dir\Admin.csproj -c "Release" -o $dir\obj\Azure\publish
