$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
echo $dir

echo "`nBuilding Bitwarden"
echo "=================="

& $dir\src\Api\build.ps1
& $dir\src\Identity\build.ps1
& $dir\util\Server\build.ps1
& $dir\util\Nginx\build.ps1
& $dir\util\Attachments\build.ps1
& $dir\src\Icons\build.ps1
& $dir\util\MsSql\build.ps1
& $dir\util\Setup\build.ps1
