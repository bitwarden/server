using System.Data;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySqlConnector;
using Npgsql;

namespace Bit.Infrastructure.IntegrationTest.Services;

public class EfMigrationTesterService : IMigrationTesterService
{
    private readonly DatabaseContext _databaseContext;
    private readonly SupportedDatabaseProviders _databaseType;
    private readonly string _migrationName;

    public EfMigrationTesterService(
        DatabaseContext databaseContext,
        SupportedDatabaseProviders databaseType,
        string migrationName)
    {
        _databaseContext = databaseContext;
        _databaseType = databaseType;
        _migrationName = migrationName;
    }

    public void ApplyMigration()
    {
        // Delete the migration history to ensure the migration is applied
        DeleteMigrationHistory();

        var migrator = _databaseContext.GetService<IMigrator>();
        migrator.Migrate(_migrationName);
    }

    private void DeleteMigrationHistory()
    {
        var deleteCommand = "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE @migrationName";
        IDbDataParameter? parameter = null;

        switch (_databaseType)
        {
            case SupportedDatabaseProviders.MySql:
                parameter = new MySqlParameter("@migrationName", "%" + _migrationName);
                break;
            case SupportedDatabaseProviders.Postgres:
                deleteCommand = "DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE @migrationName";
                parameter = new NpgsqlParameter("@migrationName", "%" + _migrationName);
                break;
            case SupportedDatabaseProviders.Sqlite:
                parameter = new SqliteParameter("@migrationName", "%" + _migrationName);
                break;
        }

        _databaseContext.Database.ExecuteSqlRaw(deleteCommand, parameter);
    }
}
