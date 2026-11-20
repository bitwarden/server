using System.Data.Common;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Migrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Infrastructure.IntegrationTest.DataMigrations;

/// <summary>
/// An empty database on a configured test database's server, migrated with the production migrators, and deleted on
/// dispose.
/// </summary>
public sealed class MigrationTestDatabase : IAsyncDisposable
{
    private readonly Database _database;
    private readonly ServiceProvider _services;

    public MigrationTestDatabase(Database template)
    {
        var name = $"vault_chain_{Guid.NewGuid():N}";
        var builder = new DbConnectionStringBuilder { ConnectionString = template.ConnectionString };
        if (template.Type == SupportedDatabaseProviders.Sqlite)
        {
            builder["Data Source"] = Path.Combine(Path.GetTempPath(), $"{name}.db");
        }
        else
        {
            builder["Database"] = name;
        }

        _database = new Database { Type = template.Type, ConnectionString = builder.ConnectionString };
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(new XUnitLoggerProvider()));
        services.AddDataProtection();
        services.SetupEntityFramework(_database.ConnectionString, _database.Type);
        services.AddDataMigrations();
        _services = services.BuildServiceProvider();
    }

    public IServiceProvider Services => _services;

    /// <summary>Migrates the database the way Admin does on a self-hosted install.</summary>
    public bool Migrate(Func<string, bool> onDataMigration) =>
        _database.Type == SupportedDatabaseProviders.SqlServer
            ? new DbMigrator(_database.ConnectionString, _services.GetRequiredService<ILogger<DbMigrator>>())
                .MigrateMsSqlDatabaseWithRetries(onDataMigration: onDataMigration)
            : ActivatorUtilities.CreateInstance<EfDbMigrator>(_services).MigrateDatabase(onDataMigration: onDataMigration);

    public async ValueTask DisposeAsync()
    {
        await using (var scope = _services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseContext>().Database.EnsureDeletedAsync();
        }

        await _services.DisposeAsync();
    }
}
