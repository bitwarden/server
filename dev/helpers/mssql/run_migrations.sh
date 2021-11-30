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

if [ ! -f "$LAST_MIGRATION_FILE" ]; then
  echo "$LAST_MIGRATION_FILE not found!"
  echo "This will run all migrations which might cause unexpected behaviour if the database is not empty."
  echo
  read -p "Run all Migrations? (y/N) " -n 1 -r
  echo
  if [[ ! $REPLY =~ ^[Yy]$ ]]
  then
      exit 1
  fi
  LAST_MIGRATION=""
else
  LAST_MIGRATION=$(cat $LAST_MIGRATION_FILE)
fi

[ -z "$LAST_MIGRATION" ]
PERFORM_MIGRATION=$?

while getopts "r" arg; do
  case $arg in
    r)
      RERUN=1
      ;;
  esac
done

if [ -n "$RERUN" ]; then
  echo "Rerunning the last migration"
fi

# Create database if it does not already exist
QUERY="IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '$DATABASE')
BEGIN
  CREATE DATABASE $DATABASE;
END;"

/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "$QUERY"

migrate () {
  local file=$1
  echo "Performing $file"
  /opt/mssql-tools/bin/sqlcmd -S $SERVER -d $DATABASE -U $USER -P $PASSWD -I -i $file
  echo $file > $LAST_MIGRATION_FILE
}

for f in `ls -v $MIGRATE_DIRECTORY/*.sql`; do
  if (( PERFORM_MIGRATION == 0 )); then
    migrate $f
  else
    echo "Skipping $f"
    if [ "$LAST_MIGRATION" == "$f" ]; then
      PERFORM_MIGRATION=0

      # Rerun last migration
      if [ -n "$RERUN" ]; then
        migrate $f
        unset RERUN
      fi
    fi
  fi
done;
