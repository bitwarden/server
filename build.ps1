$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
echo $dir

echo "`nBuilding bitwarden"
echo "=================="

& $dir\util\Server\build.ps1
& $dir\src\Api\build.ps1
& $dir\src\Identity\build.ps1
& $dir\nginx\build.ps1
& $dir\attachments\build.ps1
& $dir\util\Setup\build.ps1
