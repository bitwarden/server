// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Migrator;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.IntegrationTestCommon;

public class SqlServerTestDatabase : ITestDatabase
{
    private string _sqlServerConnection { get; set; }

    public SqlServerTestDatabase()
    {
        // Grab the connection string from the Identity project user secrets
        var identityBuilder = new ConfigurationBuilder();
        identityBuilder.AddUserSecrets(typeof(Identity.Startup).Assembly, optional: true);
        var identityConfig = identityBuilder.Build();
        var identityConnectionString = identityConfig.GetSection("globalSettings:sqlServer:connectionString").Value;

        // Replace the database name in the connection string to use a test database
        var testConnectionString = new SqlConnectionStringBuilder(identityConnectionString)
        {
            InitialCatalog = "vault_test"
        }.ConnectionString;

        _sqlServerConnection = testConnectionString;
    }

    public void ModifyGlobalSettings(Dictionary<string, string> config)
    {
        config["globalSettings:databaseProvider"] = "sqlserver";
        config["globalSettings:sqlServer:connectionString"] = _sqlServerConnection;
    }

    public void AddDatabase(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped(s => new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlServer(_sqlServerConnection)
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
        var masterConnectionString = new SqlConnectionStringBuilder(_sqlServerConnection)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        using var connection = new SqlConnection(masterConnectionString);
        var databaseName = new SqlConnectionStringBuilder(_sqlServerConnection).InitialCatalog;

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
