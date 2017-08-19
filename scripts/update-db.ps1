param (
    [string]$outputDir = "c:/bitwarden"
)

docker run -it --rm --name setup --network container:mssql -v ${outputDir}:/bitwarden bitwarden/setup `
    dotnet Setup.dll -update 1 -db 1

echo "Database update complete"
