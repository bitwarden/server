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

# Create database if it does not already exist
QUERY="IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'vault_dev')
BEGIN
  CREATE DATABASE vault_dev;
END;"

/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "$QUERY"

for f in `ls -v $MIGRATE_DIRECTORY/*.sql`; do
  if (( PERFORM_MIGRATION == 0 )); then
    echo "Performing $f"
    /opt/mssql-tools/bin/sqlcmd -S $SERVER -d $DATABASE -U $USER -P $PASSWD -I -i $f
    echo $f > $LAST_MIGRATION_FILE
  else
    echo "Skipping $f"
    [ "$LAST_MIGRATION" = "$f" ]
    PERFORM_MIGRATION=$?
  fi
done;
