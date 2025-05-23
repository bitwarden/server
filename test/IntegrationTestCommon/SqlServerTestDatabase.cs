using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Migrator;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.IntegrationTestCommon;

public class SqlServerTestDatabase : ITestDatabase
{
    public string SqlServerConnection { get; set; }

    public SqlServerTestDatabase()
    {
        SqlServerConnection = "Server=localhost;Database=vault_test;User Id=SA;Password=SET_A_PASSWORD_HERE_123;Encrypt=True;TrustServerCertificate=True;";
    }

    public void ModifyGlobalSettings(Dictionary<string, string> config)
    {
        config["globalSettings:databaseProvider"] = "sqlserver";
        config["globalSettings:sqlServer:connectionString"] = SqlServerConnection;
    }

    public void AddDatabase(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped(s => new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlServer(SqlServerConnection)
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
        var masterConnectionString = new SqlConnectionStringBuilder(SqlServerConnection)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        using var connection = new SqlConnection(masterConnectionString);
        var databaseName = new SqlConnectionStringBuilder(SqlServerConnection).InitialCatalog;

        connection.Open();

        var databaseNameQuoted = new SqlCommandBuilder().QuoteIdentifier(databaseName);

        using (var cmd = new SqlCommand($"ALTER DATABASE {databaseNameQuoted} SET single_user WITH rollback IMMEDIATE", connection))
        {
            cmd.ExecuteNonQuery();
        }

        using (var cmd = new SqlCommand($"DROP DATABASE {databaseNameQuoted}", connection))
        {
            cmd.ExecuteNonQuery();
        }
    }
}
