#!/bin/bash

# There seems to be [a bug with docker-compose](https://github.com/docker/compose/issues/4076#issuecomment-324932294) 
# where it takes ~40ms to connect to the terminal output of the container, so stuff logged to the terminal in this time is lost.
# The best workaround seems to be adding tiny delay like so:
sleep 0.1;

MIGRATE_DIRECTORY="/mnt/migrator/DbScripts"
SERVER='mssql'
DATABASE="vault_dev"
USER="SA"
PASSWD=$MSSQL_PASSWORD
RERUN_SCRIPT_NAME=""

while getopts "spr:" arg; do
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
      ;;
    r)
      RERUN_SCRIPT_NAME=$OPTARG
      ;;
  esac
done

# Create databases if they do not already exist
QUERY="IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '$DATABASE')
BEGIN
  CREATE DATABASE $DATABASE;
END;

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'migrations_$DATABASE')
BEGIN
  CREATE DATABASE migrations_$DATABASE;
END;

GO
IF OBJECT_ID('[migrations_$DATABASE].[dbo].[migrations]') IS NULL
BEGIN
    CREATE TABLE [migrations_$DATABASE].[dbo].[migrations] (
      [Id]                   INT IDENTITY(1,1) PRIMARY KEY,
      [Filename]             NVARCHAR(MAX) NOT NULL,
      [CreationDate]         DATETIME2 (7)    NULL,
    );
END;"

/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "$QUERY"

should_migrate () {
  local file=$(basename $1)
  local query="SELECT * FROM [migrations] WHERE [Filename] = '$file'"
  local result=$(/opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -Q "$query")

  if [[ "$result" =~ .*"$file".* ]]; then
    return 1;
  else
    return 0;
  fi
}

record_migration () {
  echo "Recording $1"
  local file=$(basename $1)
  local query="INSERT INTO [migrations] ([Filename], [CreationDate]) VALUES ('$file', GETUTCDATE())"
  /opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -Q "$query"
}

migrate () {
  local file=$1
  echo "Performing $file"
  /opt/mssql-tools/bin/sqlcmd -S $SERVER -d $DATABASE -U $USER -P $PASSWD -I -i $file
}


if [[ "$RERUN_SCRIPT_NAME" != "" ]]; then
  full_migration_path=$MIGRATE_DIRECTORY/$RERUN_SCRIPT_NAME
  if [[ -f $full_migration_path ]]; then
    query="SELECT * FROM [migrations] WHERE [Filename] = '$RERUN_SCRIPT_NAME'"
    result=$(/opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -Q "$query")

    if [[ "$result" =~ .*$RERUN_SCRIPT_NAME.* ]]; then
      echo "Deleting $full_migration_path"
      query="DELETE FROM [migrations] WHERE [Filename] = '$RERUN_SCRIPT_NAME'"
      result=$(/opt/mssql-tools/bin/sqlcmd -S $SERVER -d migrations_$DATABASE -U $USER -P $PASSWD -I -Q "$query")
    fi

    migrate $full_migration_path
    record_migration $full_migration_path
  fi
else
  for f in `ls -v $MIGRATE_DIRECTORY/*.sql`; do
    BASENAME=$(basename $f)
    if should_migrate $f == 1 ; then
      migrate $f
      record_migration $f
    else
      echo "Skipping $f, $BASENAME"
    fi
  done;
fi

echo "-----Migrations done-----"
