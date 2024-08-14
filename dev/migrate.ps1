#!/usr/bin/env pwsh
# Creates the vault_dev database, and runs all the migrations.

param(
  [switch]$all,
  [switch]$postgres,
  [switch]$mysql,
  [switch]$mssql,
  [switch]$sqlite,
  [switch]$selfhost
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
    # The dotnet cli command sometimes adds //BEGIN and //END comments to the output, Where-Object removes comments
    # to ensure a valid json
    return dotnet user-secrets list --json --project ../src/Api | Where-Object { $_ -notmatch "^//" } | ConvertFrom-Json
  }

  if ($selfhost) {
    $msSqlConnectionString = $(Get-UserSecrets).'dev:selfHostOverride:globalSettings:sqlServer:connectionString'
    $envName = "self-host"
  } else {
    $msSqlConnectionString = $(Get-UserSecrets).'globalSettings:sqlServer:connectionString'
    $envName = "cloud"
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
