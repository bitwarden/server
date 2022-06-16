#!/bin/bash

# There seems to be [a bug with docker-compose](https://github.com/docker/compose/issues/4076#issuecomment-324932294) 
# where it takes ~40ms to connect to the terminal output of the container, so stuff logged to the terminal in this time is lost.
# The best workaround seems to be adding tiny delay like so:
sleep 0.1;

MIGRATE_DIRECTORY="/mnt/migrator/DbScripts"
LAST_MIGRATION_FILE="/mnt/data/last_migration"
SERVER='mssql'
DATABASE="vault_dev"
USER="SA"
PASSWD=$MSSQL_PASSWORD

while getopts "s" arg; do
  case $arg in
    s)
      echo "Running for self-host environment"
      LAST_MIGRATION_FILE="/mnt/data/last_self_host_migration"
      DATABASE="vault_dev_self_host"
      ;;
  esac
done

if [ ! -f "$LAST_MIGRATION_FILE" ]; then
  echo "No migration file, nothing to migrate to a database store"
  exit 1
else
  LAST_MIGRATION=$(cat $LAST_MIGRATION_FILE)
  rm $LAST_MIGRATION_FILE
fi

[ -z "$LAST_MIGRATION" ]
PERFORM_MIGRATION=$?

# Create database if it does not already exist
QUERY="IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'migrations_$DATABASE')
BEGIN
  CREATE DATABASE migrations_$DATABASE;
END;
"

/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "$QUERY"

QUERY="IF OBJECT_ID('[dbo].[migrations_$DATABASE]') IS NULL
BEGIN
  CREATE TABLE [migrations_$DATABASE].[dbo].[migrations] (
      [Id]                   INT IDENTITY(1,1) PRIMARY KEY,
      [Filename]             NVARCHAR(MAX) NOT NULL,
      [CreationDate]         DATETIME2 (7)    NULL,
  );
END;"

/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "$QUERY"

record_migration () {
  echo "recording $1"
  local file=$(basename $1)
  echo $file
  local query="INSERT INTO [migrations] ([Filename], [CreationDate]) VALUES ('$file', GETUTCDATE())"
  /opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -Q "$query"
}

for f in `ls -v $MIGRATE_DIRECTORY/*.sql`; do
  if (( PERFORM_MIGRATION == 0 )); then
    echo "Still need to migrate $f"
  else
    record_migration $f
    if [ "$LAST_MIGRATION" == "$f" ]; then
      PERFORM_MIGRATION=0
    fi
  fi
done;
