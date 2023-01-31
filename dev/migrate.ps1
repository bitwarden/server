#!/usr/bin/env pwsh
# Creates the vault_dev database, and runs all the migrations.

# Due to azure-edge-sql not containing the mssql-tools on ARM, we manually use
#  the mssql-tools container which runs under x86_64. We should monitor this
#  in the future and investigate if we can migrate back.
# docker-compose --profile mssql exec mssql bash /mnt/helpers/run_migrations.sh @args

param(
  [switch]$all = $false,
  [switch]$postgres = $false,
  [switch]$mysql = $false,
  [switch]$mssql = $false,
  [switch]$sqlite = $false,
  [switch]$selfhost = $false,
  [switch]$pipeline = $false
)

if (!$all -and !$postgres -and !$mysql -and !$sqlite) {
  $mssql = $true;
}

if ($all -or $postgres -or $mysql -or $sqlite) {
  dotnet ef *> $null
  if ($LASTEXITCODE -ne 0) {
    Write-Host "Entity Framework Core tools were not found in the dotnet global tools. Attempting to install"
    dotnet tool install dotnet-ef -g
  }
}

if ($all -or $mssql) {
  if ($selfhost) {
    $migrationArgs = "-s"
  } elseif ($pipeline) {
    $migrationArgs = "-p"
  }

  Write-Host "Starting Microsoft SQL Server Migrations"
  docker run `
    -v "$(pwd)/helpers/mssql:/mnt/helpers" `
    -v "$(pwd)/../util/Migrator:/mnt/migrator/" `
    -v "$(pwd)/.data/mssql:/mnt/data" `
    --env-file .env `
    --network=bitwardenserver_default `
    --rm `
    mcr.microsoft.com/mssql-tools `
    /mnt/helpers/run_migrations.sh $migrationArgs
}

$currentDir = Get-Location

Foreach ($item in @(@($mysql, "MySQL", "MySqlMigrations"), @($postgres, "PostgreSQL", "PostgresMigrations"), @($sqlite, "SQLite", "SqliteMigrations"))) {
  if (!$item[0] -and !$all) {
    continue
  }

  Write-Host "Starting $($item[1]) Migrations"
  Set-Location "$currentDir/../util/$($item[2])/"
  dotnet ef database update
}

Set-Location "$currentDir"
