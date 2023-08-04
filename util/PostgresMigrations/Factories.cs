using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.PostgresMigrations;

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
        var connectionString = globalSettings.PostgreSql?.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("No Postgres connection string found.");
        }
        optionsBuilder.UseNpgsql(
            connectionString,
            b => b.MigrationsAssembly("PostgresMigrations"))
           .UseApplicationServiceProvider(serviceProvider);
        return new DatabaseContext(optionsBuilder.Options);
    }
}
