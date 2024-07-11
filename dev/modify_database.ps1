#!/usr/bin/env pwsh
param (
  [Parameter(Mandatory)]
  $Provider,
  [Parameter(Mandatory)]
  $RunMigrations
)

if (!(Test-Path ./secrets.json))
{
    Write-Output "Secrets file doesn't exist, creating one from example."
    Copy-Item ./secrets.json.example -Destination ./secrets.json
}

$secrets = Get-Content -Raw ./secrets.json | ConvertFrom-Json

if ($Env:CODESPACES)
{
    Write-Output "Running in Codespaces"
    $RunningEnvironment = "Codespaces"
}
elseif ($Env:HOSTNAME)
{
    Write-Output "Running in devcontainer maybe? ($Env:HOSTNAME)"
    $RunningEnvironment = "DevContainer"
}
else
{
    $RunningEnvironment = "HostMachine"
}

function Ensure-Container($Profile) {
  Write-Output "Adding container $Profile for env $RunningEnvironment"
  if ($RunningEnvironment -eq "DevContainer")
  {
      $ProjectName = $(docker inspect $Env:HOSTNAME --format '{{ index .Config.Labels "com.docker.compose.project" }}')

      # Should I check for an existing container already?

      # CURRENT PROBLEM WITH THIS:
      # Error response from daemon: Mounts denied: 
      # The path /workspace/dev/.data/postgres/config is not shared from the host and is not known to Docker.
      # You can configure shared paths from Docker -> Preferences... -> Resources -> File Sharing.
      # See https://docs.docker.com/desktop/mac for more info.

      # Patch in the container from the local docker-compose.yml file with the matching profile name
      # into the compose project for the devcontainer, this does leave the new container as an orphan though
      docker compose -p $ProjectName --profile $Profile -f docker-compose.yml up -d
  }
  else
  {
      Write-Warning "Adding container in environment '$RunningEnvironment' is not supported."
  }
}

function Save-Secrets {
  $secrets | ConvertTo-Json -depth 100 | Out-File "./secrets.json"
  Write-Warning "You will want to run the Set Up Secrets task or ./setup_secrets.ps1"
}

if ($Provider -eq "do-not-change")
{
    $Provider = $secrets.globalSettings.databaseProvider
}

Write-Output "Running $Provider with $RunMigrations"

switch ( $Provider )
{
    sqlite
    {
        Write-Output "Modifying Sqlite"
        $secrets.globalSettings.databaseProvider = "sqlite"
        # Check if it's null or still the example default
        if (($secrets.globalSettings.sqlite.connectionString -eq $null) -or ($secrets.globalSettings.sqlite.connectionString -eq "Data Source=/path/to/bitwardenServer/repository/server/dev/db/bitwarden.sqlite"))
        {
            $secrets.globalSettings.sqlite | Add-Member -Name "connectionString" -Value "Data Source=$pwd/sqlite.db" -MemberType NoteProperty
        }
        # dotnet tool restore? Maybe in postCreate?
        if ( $RunMigrations )
        {
            Write-Output "Running migration for connection string: $($secrets.globalSettings.sqlite.connectionString)"
            Push-Location "../util/SqliteMigrations"
            dotnet ef database update --connection $secrets.globalSettings.sqlite.connectionString -- --GlobalSettings:Sqlite:ConnectionString="$secrets.globalSettings.sqlite.connectionString"
            Pop-Location
        }
        Save-Secrets
    }
    postgresql
    {
        Write-Output "Modifying PostgreSQL"
        $secrets.globalSettings.databaseProvider = "postgresql"
        # Attempt to set a reasonable connection string if it's null
        Ensure-Container -Profile postgres
    }
    sqlserver
    {
        Write-Output "Modifying SQL Server"
        $secrets.globalSettings.databaseProvider = "sqlserver"
        # Attempt to set a reasonable connection string if it's null
    }
    mysql
    {
        Write-Output "Modifying MySql"
        $secrets.globalSettings.databaseProvider = "mysql"
        # Attempt to set a reasonable connection string if it's null
    }
}
