#!/usr/bin/env pwsh
param (
  [Parameter(Mandatory)]
  $Name
)

# DB service provider name
$service = "mysql"

Write-Output "--- Attempting to start $service service ---"

# Attempt to start mysql but if docker-compose doesn't
# exist just trust that the user has it running some other way
if (command -v docker-compose) {
  docker-compose --profile $service up -d --no-recreate
}

dotnet tool restore

$providers = @{
    MySql = "../util/MySqlMigrations"
    Postgres = "../util/PostgresMigrations"
    Sqlite = "../util/SqliteMigrations"
}

foreach ($key in $providers.keys) {
    Write-Output "--- START $key ---"
    dotnet ef migrations add $Name -s $providers[$key]
    Write-Output "--- END $key ---"
}
