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

/// <summary>
/// An implementation of <see cref="IMigrationTesterService"/> for testing Entity Framework migrations.
/// This service applies a specific migration and manages the migration history
/// to ensure that the migration is tested in isolation. It supports MySQL, Postgres, and SQLite.
/// </summary>
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

        if (_databaseType == SupportedDatabaseProviders.Sqlite)
        {
            // SQLite doesn't support DropColumn, so we can't use Migrate() which
            // reverts subsequent migrations (calling their Down() methods).
            // Instead, generate and execute only the target migration's Up() SQL.
            var migrationsAssembly = _databaseContext.GetService<IMigrationsAssembly>();
            var migrations = migrationsAssembly.Migrations.Keys.ToList();
            var targetMigrationId = migrations.SingleOrDefault(m => m.EndsWith(_migrationName))
                ?? throw new InvalidOperationException(
                    $"Migration '{_migrationName}' was not found in the migrations assembly.");
            var targetIndex = migrations.IndexOf(targetMigrationId);
            var previousMigrationId = targetIndex > 0 ? migrations[targetIndex - 1] : "0";

            var script = migrator.GenerateScript(previousMigrationId, targetMigrationId);
            _databaseContext.Database.ExecuteSqlRaw(script);
        }
        else
        {
            migrator.Migrate(_migrationName);
        }
    }

    /// <summary>
    /// Deletes the migration history for the specified migration name.
    /// </summary>
    private void DeleteMigrationHistory()
    {
        var deleteCommand = "DELETE FROM __EFMigrationsHistory WHERE MigrationId LIKE @migrationName";
        IDbDataParameter? parameter;

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
            default:
                throw new InvalidOperationException($"Unsupported database type: {_databaseType}");
        }

        _databaseContext.Database.ExecuteSqlRaw(deleteCommand, parameter);
    }
}
