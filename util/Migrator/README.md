# Bitwarden Database Migrator

A class library leveraged by [utilities](../MsSqlMigratorUtility) and [hosted applications](/src/Admin/HostedServices/DatabaseMigrationHostedService.cs) to perform SQL database migrations. A [MSSQL migrator](./SqlServerDbMigrator.cs) exists here as the default use case.

In production environments the Migrator is typically executed during application startup or as part of CI/CD pipelines to ensure database schemas are up-to-date before application deployment.

See the [documentation on creating migrations](https://contributing.bitwarden.com/contributing/database-migrations/) for how to utilize the files seen here.
