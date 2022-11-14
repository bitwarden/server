using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Bit.PostgresMigrations;

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
        var globalSettings = GlobalSettingsFactory.GlobalSettings;
        var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
        var connectionString = globalSettings.PostgreSql?.ConnectionString;
        // NpgSql 6.0 changed how timezones works. We have not yet updated our projects to support this new behavior and need to fallback to the previous behavior.
        // Check https://www.npgsql.org/doc/release-notes/6.0.html#timestamp-rationalization-and-improvements for more details.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("No Postgres connection string found.");
        }
        optionsBuilder.UseNpgsql(
            connectionString,
            b => b.MigrationsAssembly("PostgresMigrations"));
        return new DatabaseContext(optionsBuilder.Options);
    }
}
