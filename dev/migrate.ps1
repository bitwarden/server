# Creates the vault_dev database, and runs all the migrations.

docker-compose -f .\docker-compose.yml --profile mssql exec mssql bash /mnt/helpers/migrate.sh @args
