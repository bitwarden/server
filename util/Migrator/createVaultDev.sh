# Creates and populates vault_dev - used for development purposes
# This should be run from within an empty MSSQL Docker container
# See instructions in SETUP.md

if [ -z $1 ]; then
  echo "Error: you must provide SA_PASSWORD as the first argument."
  echo "You should wrap your password in single quotes to make sure it is correctly interpreted."
  exit 1
fi

MIGRATE_DIRECTORY="/mnt/migrator/DbScripts/"
SERVER="localhost"
DATABASE="vault_dev"
USER="sa"
PASSWD="$1"

/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "CREATE DATABASE $DATABASE;"

for f in `ls -v $MIGRATE_DIRECTORY/*.sql`; do
  /opt/mssql-tools/bin/sqlcmd -S $SERVER -d $DATABASE -U $USER -P $PASSWD -I -i $f
done;
