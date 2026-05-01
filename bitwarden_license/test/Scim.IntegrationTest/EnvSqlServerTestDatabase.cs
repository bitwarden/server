using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.IntegrationTestCommon;
using Bit.Migrator;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Bit.Scim.IntegrationTest;

/// <summary>
/// SQL Server test database that resolves its connection string from the same
/// BW_TEST_DATABASES__n__CONNECTIONSTRING env vars used by DatabaseDataAttribute,
/// with a fallback to Identity user secrets for local dev.
/// </summary>
public class EnvSqlServerTestDatabase : ITestDatabase
{
    private readonly string _connectionString;

    public EnvSqlServerTestDatabase()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Identity.Startup).Assembly, optional: true)
            .AddEnvironmentVariables("BW_TEST_")
            .Build();

        var resolved =
            config.Get<TypedConfig>()?.Databases?
                .FirstOrDefault(d => d.Type == SupportedDatabaseProviders.SqlServer)?.ConnectionString
            ?? config.GetSection("globalSettings:sqlServer:connectionString").Value
            ?? throw new InvalidOperationException(
                "No SQL Server connection string found. Set BW_TEST_DATABASES__n__TYPE=SqlServer " +
                "and BW_TEST_DATABASES__n__CONNECTIONSTRING, or configure Identity user secrets locally.");

        _connectionString = new SqlConnectionStringBuilder(resolved)
        {
            InitialCatalog = "vault_test"
        }.ConnectionString;
    }

    public void ModifyGlobalSettings(Dictionary<string, string?> config)
    {
        config["globalSettings:databaseProvider"] = "sqlserver";
        config["globalSettings:sqlServer:connectionString"] = _connectionString;
    }

    public void AddDatabase(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped(s => new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlServer(_connectionString)
            .UseApplicationServiceProvider(s)
            .Options);
    }

    public void Migrate(IServiceCollection serviceCollection)
    {
        var serviceProvider = serviceCollection.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var globalSettings = services.GetRequiredService<GlobalSettings>();
        var logger = services.GetRequiredService<ILogger<DbMigrator>>();

        var migrator = new SqlServerDbMigrator(globalSettings, logger);
        migrator.MigrateDatabase();
    }

    public void Dispose()
    {
        var masterConnectionString = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        using var connection = new SqlConnection(masterConnectionString);
        var databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;

        connection.Open();

        var databaseNameQuoted = new SqlCommandBuilder().QuoteIdentifier(databaseName);

        using (var cmd = new SqlCommand(
            $"ALTER DATABASE {databaseNameQuoted} SET single_user WITH rollback IMMEDIATE", connection))
        {
            cmd.ExecuteNonQuery();
        }

        using (var cmd = new SqlCommand($"DROP DATABASE {databaseNameQuoted}", connection))
        {
            cmd.ExecuteNonQuery();
        }
    }

    private class TypedConfig
    {
        public DatabaseEntry[]? Databases { get; set; }
    }

    private class DatabaseEntry
    {
        public SupportedDatabaseProviders Type { get; set; }
        public string ConnectionString { get; set; } = default!;
    }
}
