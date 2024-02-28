# Uses our MsSqlMigratorUtility to run migrations on our local dev database.
# WARNING: DO NOT USE if your dev database has been set up using our migrate.ps1 script.
# This should only be used on a new database.

# TODO:
# - call this from migrate.ps1
# - figure out any migration pathway for existing dev database
# - support self-host / arbitrary database name

# Abort on any error
$ErrorActionPreference = "Stop"

# Load the SQL assembly - this avoids using sqlcmd which is not available on all platforms
# It's the same assembly used by our server project so it should be available on all dev machines
Add-Type -AssemblyName "System.Data.SqlClient"

# Get connection string from user secrets
# TODO: support self-hosted option
$userSecrets = dotnet user-secrets list --json --project ../src/Api | ConvertFrom-Json
$connectionString = $userSecrets.'globalSettings:sqlServer:connectionString'
$database = "vault_dev" # TODO: get this from the connection string?

function Create-Database {
  $connection = New-Object System.Data.SqlClient.SqlConnection
  # connectionString excludes the database name as it may not exist yet
  $connection.ConnectionString = $connectionString -replace "Database=$database;"
  $query="IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '$database')
  BEGIN
    CREATE DATABASE $database;
  END;
  "
  $command = $connection.CreateCommand()
  $command.CommandText = $query

  # Execute the command
  try {
      $connection.Open()
      $command.ExecuteNonQuery()
  }
  catch {
      Write-Error "Error creating database: $_"
  }
  finally {
      $connection.Close()
  }
}

Create-Database
dotnet run --project ../util/MsSqlMigratorUtility/ "$connectionString"
