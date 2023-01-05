using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Bit.SqliteMigrations;

public static class GlobalSettingsFactory
{
    public static GlobalSettings GlobalSettings { get; } = new GlobalSettings();
    static GlobalSettingsFactory()
    {
        var configBuilder = new ConfigurationBuilder().AddUserSecrets("bitwarden-Api");
        var Configuration = configBuilder.Build();
        ConfigurationBinder.Bind(Configuration.GetSection("GlobalSettings"), GlobalSettings);
    }
}

public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
{
    public DatabaseContext CreateDbContext(string[] args)
    {
        var globalSettings = GlobalSettingsFactory.GlobalSettings;
        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        var connectionString = globalSettings.Sqlite?.ConnectionString ?? "Data Source=:memory:";
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("No Sqlite connection string found.");
        }
        optionsBuilder.UseSqlite(
            connectionString,
            b => b.MigrationsAssembly("SqliteMigrations"));
        return new DatabaseContext(optionsBuilder.Options);
    }
}
