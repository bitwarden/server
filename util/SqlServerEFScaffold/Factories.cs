using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SqlServerEFScaffold;

public static class GlobalSettingsFactory
{
    public static GlobalSettings GlobalSettings { get; } = new GlobalSettings();
    static GlobalSettingsFactory()
    {
        var configBuilder = new ConfigurationBuilder().AddUserSecrets<Bit.Api.Startup>();
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
        var connectionString = globalSettings.SqlServer?.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception("No SqlServer connection string found.");
        }
        optionsBuilder.UseSqlServer(
            connectionString,
            b => b.MigrationsAssembly("SqlServerEFScaffold"));
        return new DatabaseContext(optionsBuilder.Options);
    }
}
