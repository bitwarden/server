#!/usr/bin/env pwsh
param (
  [Parameter(Mandatory)]
  $Name
)

# Set docker compose path
$dockerPath = "docker-compose.yml"

# DB service provider names
$mysqlService = "mysql"
$postgresService = "postgres"

# Get container ids
$mysqlContainerId = docker-compose -f $dockerPath ps -q $mysqlService
$postgresContainerId = docker-compose -f $dockerPath ps -q $postgresService

# Get status 
$mysqlStatus = docker container inspect --format "{{.State.Status}}" $mysqlContainerId
$postgresStatus = docker container inspect --format "{{.State.Status}}" $postgresContainerId


if ($mysqlStatus -ne "running") {
    Write-Output "--- Attempting to start $mysqlService service ---"
    # Start the container
    docker container start $mysqlContainerId
}

if ($postgresStatus -ne "running") {
    Write-Output "--- Attempting to start $postgresService service ---"
    # Start the container
    docker container start $postgresContainerId
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
