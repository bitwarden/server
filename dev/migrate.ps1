#!/usr/bin/env pwsh
# Creates the vault_dev database, and runs all the migrations.

docker-compose --profile mssql exec mssql bash /mnt/helpers/run_migrations.sh @args
