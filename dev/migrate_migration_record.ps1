#!/usr/bin/env pwsh
# This script need only be run once
#
# This is a migration script for updating recording the last migration run
# in a file to recording migrations in a database table. It will create a
# migrations_vault table and store all of the previously run migrations as
# indicated by a last_migrations file. It will then delete this file.

# Due to azure-edge-sql not containing the mssql-tools on ARM, we manually use
#  the mssql-tools container which runs under x86_64. We should monitor this
#  in the future and investigate if we can migrate back.
# docker-compose --profile mssql exec mssql bash /mnt/helpers/run_migrations.sh @args

docker run `
  -v "$(pwd)/helpers/mssql:/mnt/helpers" `
  -v "$(pwd)/../util/Migrator:/mnt/migrator/" `
  -v "$(pwd)/.data/mssql:/mnt/data" `
  --env-file .env `
  --network=bitwardenserver_default `
  --rm `
  -it `
  mcr.microsoft.com/mssql-tools `
  /mnt/helpers/migrate_migrations.sh @args
