# Creates and populates vault_dev - used for development purposes
# This should be run from within an empty MSSQL Docker container
# See instructions in CONTRIBUTING.md

MIGRATE_DIRECTORY="/mnt/migrator/DbScripts/"
SERVER="localhost"
DATABASE="vault_dev"
USER="sa"
PASSWD="YOUR_SA_PASSWORD"   # insert your SA password

/opt/mssql-tools/bin/sqlcmd -S $SERVER -d master -U $USER -P $PASSWD -I -Q "CREATE DATABASE $DATABASE;"

for f in `ls -v $MIGRATE_DIRECTORY/*.sql`; do
  /opt/mssql-tools/bin/sqlcmd -S $SERVER -d $DATABASE -U $USER -P $PASSWD -I -i $f
done;
