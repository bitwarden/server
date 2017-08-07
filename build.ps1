$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
echo $dir

echo "`nBuilding bitwarden"
echo "=================="

& $dir\src\Api\build.ps1
& $dir\src\Identity\build.ps1
& $dir\nginx\build.ps1
