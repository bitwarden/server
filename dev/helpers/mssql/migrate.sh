#!/bin/bash

MIGRATE_DIRECTORY="/mnt/migrator/DbScripts"
LAST_MIGRATION_FILE="/mnt/data/last_migration"
SERVER='localhost'
DATABASE="vault_dev"
USER="SA"
PASSWD=$SA_PASSWORD

LAST_MIGRATION=$(cat $LAST_MIGRATION_FILE)
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
QUERY="IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'vault_dev')
BEGIN
  CREATE DATABASE vault_dev;
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
