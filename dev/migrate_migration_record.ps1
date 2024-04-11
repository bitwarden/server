#!/usr/bin/env pwsh
# !!! UPDATED 2024 for MsSqlMigratorUtility !!!
#
# This is a migration script to move data from [migrations_vault_dev].[dbo].[migrations] (used by our custom
# migrator script) to [vault_dev].[dbo].[Migration] (used by MsSqlMigratorUtility). It is safe to run multiple
# times because it will not perform any migration if it detects that the new table is already present.
# This will be deleted after a few months after everyone has (presumably) migrated to the new schema.

# Due to azure-edge-sql not containing the mssql-tools on ARM, we manually use
# the mssql-tools container which runs under x86_64.

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
