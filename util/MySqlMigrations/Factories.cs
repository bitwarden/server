using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.MySqlMigrations;

public static class GlobalSettingsFactory
{
    public static GlobalSettings GlobalSettings { get; } = new GlobalSettings();
    static GlobalSettingsFactory()
    {
        // UserSecretsId here should match what is in Api.csproj
        var configBuilder = new ConfigurationBuilder().AddUserSecrets("bitwarden-Api");
        var Configuration = configBuilder.Build();
        ConfigurationBinder.Bind(Configuration.GetSection("GlobalSettings"), GlobalSettings);
    }
}

public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var serviceProvider = services.BuildServiceProvider();

        var globalSettings = GlobalSettingsFactory.GlobalSettings;
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
