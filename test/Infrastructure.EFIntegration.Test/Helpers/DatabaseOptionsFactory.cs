using Bit.Core.Test.Helpers.Factories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bit.Infrastructure.EFIntegration.Test.Helpers;

public static class DatabaseOptionsFactory
{
    public static List<DbContextOptions<DatabaseContext>> Options { get; } = new();

    static DatabaseOptionsFactory()
    {
        var globalSettings = GlobalSettingsFactory.GlobalSettings;
        if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.PostgreSql?.ConnectionString))
        {
            Options.Add(new DbContextOptionsBuilder<DatabaseContext>().UseNpgsql(globalSettings.PostgreSql.ConnectionString).Options);
        }
        if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.MySql?.ConnectionString))
        {
            var mySqlConnectionString = globalSettings.MySql.ConnectionString;
            Options.Add(new DbContextOptionsBuilder<DatabaseContext>().UseMySql(mySqlConnectionString, ServerVersion.AutoDetect(mySqlConnectionString)).Options);
        }
    }
}
