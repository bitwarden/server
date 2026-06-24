using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bit.IntegrationTestCommon;

public class PostgresTestDatabase : ITestDatabase
{
    private readonly string _adminConnectionString;
    private readonly string _testConnectionString;
    private readonly string _databaseName;

    public PostgresTestDatabase(string baseConnectionString, string databaseName = "vault_test")
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        _adminConnectionString = baseConnectionString;
        _databaseName = databaseName;
        _testConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = _databaseName,
        }.ConnectionString;
    }

    public void ModifyGlobalSettings(Dictionary<string, string?> config)
    {
        config["globalSettings:databaseProvider"] = "postgres";
        config["globalSettings:postgreSql:connectionString"] = _testConnectionString;
    }

    public void AddDatabase(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped(s => new DbContextOptionsBuilder<DatabaseContext>()
            .UseNpgsql(_testConnectionString, b => b.MigrationsAssembly("PostgresMigrations"))
            .UseApplicationServiceProvider(s)
            .Options);
    }

    public void Migrate(IServiceCollection serviceCollection)
    {
        EnsureDatabaseExists();

        var serviceProvider = serviceCollection.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        context.Database.Migrate();
    }

    public void Dispose()
    {
        // Connection pool retains physical connections to the test DB after factory disposal.
        // We don't drop the database (it's persistent across runs) but we clear the pool so a
        // subsequent run with different settings doesn't reuse stale connections.
        NpgsqlConnection.ClearAllPools();
    }

    private void EnsureDatabaseExists()
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @dbname", connection);
        checkCmd.Parameters.AddWithValue("dbname", _databaseName);
        if (checkCmd.ExecuteScalar() is not null)
        {
            return;
        }

        using var createCmd = new NpgsqlCommand($"CREATE DATABASE {QuoteIdentifier(_databaseName)}", connection);
        createCmd.ExecuteNonQuery();
    }

    private static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
