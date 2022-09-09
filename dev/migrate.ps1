#!/usr/bin/env pwsh
# Creates the vault_dev database, and runs all the migrations.

# Due to azure-edge-sql not containing the mssql-tools on ARM, we manually use
#  the mssql-tools container which runs under x86_64. We should monitor this
#  in the future and investigate if we can migrate back.
# docker-compose --profile mssql exec mssql bash /mnt/helpers/run_migrations.sh @args

param([switch]$all = $false, [switch]$postgres = $false, [switch]$mysql = $false)

if ($all) {
  Write-Host "Starting Microsoft SQL Server Migrations"
}

if ($all -or (!$postgres -and !$mysql)) {
  docker run `
    -v "$(pwd)/helpers/mssql:/mnt/helpers" `
    -v "$(pwd)/../util/Migrator:/mnt/migrator/" `
    -v "$(pwd)/.data/mssql:/mnt/data" `
    --env-file .env `
    --network=bitwardenserver_default `
    --rm `
    mcr.microsoft.com/mssql-tools `
    /mnt/helpers/run_migrations.sh @args
}

$currentDir = Get-Location

if ($all -or $mysql) {
  Write-Host "Starting MySQL Migrations"
  Set-Location "$currentDir/../util/MySqlMigrations/"
  dotnet ef database update
}
if ($all -or $postgres) {
  Write-Host "Starting PostgreSQL Migrations"
  Set-Location "$currentDir/../util/PostgresMigrations/"
  dotnet ef database update
}