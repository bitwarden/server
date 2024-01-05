#!/bin/bash

# There seems to be [a bug with docker-compose](https://github.com/docker/compose/issues/4076#issuecomment-324932294)
# where it takes ~40ms to connect to the terminal output of the container, so stuff logged to the terminal in this time is lost.
# The best workaround seems to be adding tiny delay like so:
sleep 0.1;

MIGRATE_DIRECTORY="/mnt/migrator/DbScripts"
HELPERS_DIRECTORY="/mnt/helpers"
SERVER='mssql'
DATABASE="vault_dev"
USER="SA"
PASSWD=$MSSQL_PASSWORD

while getopts "sp" arg; do
  case $arg in
    s)
      echo "Running for self-host environment"
      DATABASE="vault_dev_self_host"
      ;;
    p)
      echo "Running for pipeline"
      MIGRATE_DIRECTORY=$MSSQL_MIGRATIONS_DIRECTORY
      SERVER=$MSSQL_HOST
      DATABASE=$MSSQL_DATABASE
      USER=$MSSQL_USER
      PASSWD=$MSSQL_PASS
  esac
done

# Create databases if they do not already exist
QUERY="IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '$DATABASE')
BEGIN
  CREATE DATABASE $DATABASE;
END;

GO
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'migrations_$DATABASE')
BEGIN
  CREATE DATABASE migrations_$DATABASE;
END;

GO
"
/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "$QUERY"
echo "Return code: $?"

# Create migrations table if it does not already exist
QUERY="IF OBJECT_ID('[migrations_$DATABASE].[dbo].[migrations]') IS NULL
BEGIN
    CREATE TABLE [migrations_$DATABASE].[dbo].[migrations] (
      [Id]                   INT IDENTITY(1,1) PRIMARY KEY,
      [Filename]             NVARCHAR(MAX) NOT NULL,
      [CreationDate]         DATETIME2 (7)    NULL,
    );
END;
GO
"
/opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -Q "$QUERY"
echo "Return code: $?"

# Create or update the ReadRequiredMigrations sproc every time for simplicity
/opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -i "$HELPERS_DIRECTORY/read_required_migrations.sql"

record_migration () {
  echo "recording $1"
  local file=$(basename $1)
  echo $file
  local query="INSERT INTO [migrations] ([Filename], [CreationDate]) VALUES ('$file', GETUTCDATE())"
  /opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -Q "$query"
}

migrate () {
  local file=$1
  echo "Performing $file"
  /opt/mssql-tools/bin/sqlcmd -S $SERVER -d $DATABASE -U $USER -P $PASSWD -I -i $file
}

get_migrations_to_run() {
  # get a semicolon delimited list of all migrations
  # this exceeds the max command length, so we save it to a file that MSSQL can use to bulk insert from
  # tr replaces newlines with semicolons
  # sed removes the trailing semicolon
  echo -n `(cd $MIGRATE_DIRECTORY && ls -1 *.sql) | tr '\n' ';' | sed 's/;$//'` > "$HELPERS_DIRECTORY/all_migrations.txt"

  ## this query returns a space delimited list of migrations that need to be run
  local query="EXEC ReadRequiredMigrations '$HELPERS_DIRECTORY/all_migrations.txt'"
  echo `/opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -Q "$query" -W -h-1`
}

MIGRATIONS_TO_RUN=$(get_migrations_to_run)
for f in $MIGRATIONS_TO_RUN; do
  migrate "$MIGRATE_DIRECTORY/$f"
  record_migration $f
done;
