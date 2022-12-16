#!/usr/bin/env pwsh
param (
  [Parameter(Position = 1)]
  $Name
)

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
