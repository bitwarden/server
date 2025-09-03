Set-Location "$PSScriptRoot/.."

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:swaggerGen = "True"
$env:DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX = "2"
$env:GLOBALSETTINGS__SQLSERVER__CONNECTIONSTRING = "placeholder"

dotnet tool restore

# Identity
Set-Location "./src/Identity"
dotnet build
dotnet swagger tofile --output "../../identity.json" --host "https://identity.bitwarden.com" "./bin/Debug/net8.0/Identity.dll" "v1"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# Api internal & public
Set-Location "../../src/Api"
dotnet build
dotnet swagger tofile --output "../../api.json" --host "https://api.bitwarden.com" "./bin/Debug/net8.0/Api.dll" "internal"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
dotnet swagger tofile --output "../../api.public.json" --host "https://api.bitwarden.com" "./bin/Debug/net8.0/Api.dll" "public"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
