#!/usr/bin/env pwsh
# Creates the vault_dev database, and runs all the migrations.

param(
  [switch]$all,
  [switch]$postgres,
  [switch]$mysql,
  [switch]$mariadb,
  [switch]$mssql,
  [switch]$sqlite,
  [switch]$selfhost,
  [switch]$test
)

# Abort on any error
$ErrorActionPreference = "Stop"
$currentDir = Get-Location

function Get-IsEFDatabase {
  return $postgres -or $mysql -or $mariadb -or $sqlite;
}

if (!$all -and !$(Get-IsEFDatabase)) {
  $mssql = $true;
}

if ($all -or $(Get-IsEFDatabase)) {
  dotnet ef *> $null
  if ($LASTEXITCODE -ne 0) {
    Write-Host "Entity Framework Core tools were not found in the dotnet global tools. Attempting to install"
    dotnet tool install dotnet-ef -g
  }
}

function Get-UserSecrets {
  # The dotnet cli command sometimes adds //BEGIN and //END comments to the output, Where-Object removes comments
  # to ensure a valid json
  return dotnet user-secrets list --json --project "$currentDir/../src/Api" | Where-Object { $_ -notmatch "^//" } | ConvertFrom-Json
}

if ($all -or $mssql) {
  if ($all -or !$test) {
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

  if ($all -or $test) {
    $testMsSqlConnectionString = $(Get-UserSecrets).'databases:3:connectionString'
    if ($testMsSqlConnectionString) {
      $testEnvName = "test databases"
      Write-Host "Starting Microsoft SQL Server Migrations for $testEnvName"
      dotnet run --project ../util/MsSqlMigratorUtility/ "$testMsSqlConnectionString"
    } else {
      Write-Host "Connection string for a test MSSQL database not found in secrets.json!"
    }
  }
}

Foreach ($item in @(
    @($postgres, "PostgreSQL", "PostgresMigrations", "postgreSql", 0),
    @($sqlite, "SQLite", "SqliteMigrations", "sqlite", 1),
    @($mysql, "MySQL", "MySqlMigrations", "mySql", 2),
    # MariaDB shares the MySQL connection string in the server config so they are mutually exclusive in that context.
    # However they can still be run independently for integration tests.
    @($mariadb, "MariaDB", "MySqlMigrations", "mySql", 3) 
)) {
  if (!$item[0] -and !$all) {
    continue
  }

  Set-Location "$currentDir/../util/$($item[2])/"
  if(!$test -or $all) {
    Write-Host "Starting $($item[1]) Migrations"
    $connectionString = $(Get-UserSecrets)."globalSettings:$($item[3]):connectionString"
    dotnet ef database update --connection "$connectionString"
  }
  if ($test -or $all) {
    $testConnectionString = $(Get-UserSecrets)."databases:$($item[4]):connectionString"
    if ($testConnectionString) {
      Write-Host "Starting $($item[1]) Migrations for test databases"
      dotnet ef database update --connection "$testConnectionString"
    } else {
      Write-Host "Connection string for a test $($item[1]) database not found in secrets.json!"
    }
  }
}

Set-Location "$currentDir"
