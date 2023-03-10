using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.MySqlMigrations;

public class GlobalSettingsFactory
{
    public GlobalSettings GlobalSettings { get; }

    public GlobalSettingsFactory(string[] args)
    {
        GlobalSettings = new GlobalSettings();
        // UserSecretsId here should match what is in Api.csproj
        var config = new ConfigurationBuilder()
            .AddUserSecrets("bitwarden-Api")
            .AddCommandLine(args)
            .Build();

        // TODO: Remove before merging
        Console.WriteLine(config.GetDebugView());

        config.GetSection("GlobalSettings").Bind(GlobalSettings);
    }
}

public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var serviceProvider = services.BuildServiceProvider();

        var globalSettings = new GlobalSettingsFactory(args)
            .GlobalSettings;

        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        var connectionString = globalSettings.MySql?.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("No MySql connection string found.");
        }
        optionsBuilder.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(connectionString),
            b => b.MigrationsAssembly("MySqlMigrations"))
           .UseApplicationServiceProvider(serviceProvider);
        return new DatabaseContext(optionsBuilder.Options);
    }
}
