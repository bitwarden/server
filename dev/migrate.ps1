# Creates the vault_dev database, and runs all the migrations.

## TODO: We should add a --rerun-last argument or something to help local development


docker-compose -f .\docker-compose.mssql.yml exec db bash /mnt/helpers/migrate.sh
