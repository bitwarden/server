#!/usr/bin/env pwsh
# Creates the vault_dev database, and runs all the migrations.

# Due to azure-edge-sql not containing the mssql-tools on ARM, we manually use
#  the mssql-tools container which runs under x86_64.

param(
  [switch]$all,
  [switch]$postgres,
  [switch]$mysql,
  [switch]$mssql,
  [switch]$sqlite,
  [switch]$selfhost,
  [switch]$pipeline
)

# Abort on any error
$ErrorActionPreference = "Stop"

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
  function Get-UserSecrets {
    return dotnet user-secrets list --json --project ../src/Api | ConvertFrom-Json
  }

  if ($selfhost) {
    $msSqlConnectionString = $(Get-UserSecrets).'dev:selfHostOverride:globalSettings:sqlServer:connectionString'
    $envName = "self-host"

    Write-Output "Migrating your migrations to use MsSqlMigratorUtility (if needed)"
    ./migrate_migration_record.ps1 -s
  } elseif ($pipeline) {
    # pipeline sets this through an environment variable, see test-database.yml
    $msSqlConnectionString = "$Env:CONN_STR"
    $envName = "pipeline"
  } else {
    $msSqlConnectionString = $(Get-UserSecrets).'globalSettings:sqlServer:connectionString'
    $envName = "cloud"

    Write-Output "Migrating your migrations to use MsSqlMigratorUtility (if needed)"
    ./migrate_migration_record.ps1
  }

  Write-Host "Starting Microsoft SQL Server Migrations for $envName"

  dotnet run --project ../util/MsSqlMigratorUtility/ "$msSqlConnectionString"
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
