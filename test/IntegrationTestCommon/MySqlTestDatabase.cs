using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace Bit.IntegrationTestCommon;

public class MySqlTestDatabase : ITestDatabase
{
    private readonly string _adminConnectionString;
    private readonly string _testConnectionString;
    private readonly string _databaseName;
    private readonly ServerVersion _serverVersion;

    public MySqlTestDatabase(string baseConnectionString, string databaseName = "vault_test")
    {
        _adminConnectionString = baseConnectionString;
        // AutoDetect opens a connection — must point at an existing DB (the admin endpoint)
        _serverVersion = ServerVersion.AutoDetect(_adminConnectionString);

        _databaseName = databaseName;
        _testConnectionString = new MySqlConnectionStringBuilder(baseConnectionString)
        {
            Database = _databaseName,
        }.ConnectionString;
    }

    public void ModifyGlobalSettings(Dictionary<string, string?> config)
    {
        config["globalSettings:databaseProvider"] = "mysql";
        config["globalSettings:mySql:connectionString"] = _testConnectionString;
    }

    public void AddDatabase(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped(s => new DbContextOptionsBuilder<DatabaseContext>()
            .UseMySql(_testConnectionString, _serverVersion, b => b.MigrationsAssembly("MySqlMigrations"))
            .UseApplicationServiceProvider(s)
            .Options);
    }

    public void Migrate(IServiceCollection serviceCollection)
    {
        using (var connection = new MySqlConnection(_adminConnectionString))
        {
            connection.Open();
            using var cmd = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS {QuoteIdentifier(_databaseName)}", connection);
            cmd.ExecuteNonQuery();
        }

        var serviceProvider = serviceCollection.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        context.Database.Migrate();
    }

    public void Dispose()
    {
        // Don't drop the test DB — it's persistent across runs. Clear the pool so the
        // next run isn't holding stale physical connections.
        MySqlConnection.ClearAllPools();
    }

    private static string QuoteIdentifier(string identifier) =>
        "`" + identifier.Replace("`", "``") + "`";
}
