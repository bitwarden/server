using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Test.Common.Helpers.Factories;
using Microsoft.EntityFrameworkCore;

namespace Bit.Core.Test.Helpers.Factories
{
    // Keep the order of TestingDatabaseProviderOrder in sync with DatabaseOptionsFactory
    public enum TestingDatabaseProviderOrder
    {
        Postgres = 0,
        MySql = 1,
        SqlServer = 2
    }

    public static class DatabaseOptionsFactory
    {
        public static Dictionary<TestingDatabaseProviderOrder, DbContextOptions<DatabaseContext>> Options { get; } = new();

        static DatabaseOptionsFactory()
        {
            var globalSettings = GlobalSettingsFactory.GlobalSettings;
            if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.PostgreSql?.ConnectionString))
            {
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                Options.Add(TestingDatabaseProviderOrder.Postgres, new DbContextOptionsBuilder<DatabaseContext>().UseNpgsql(globalSettings.PostgreSql.ConnectionString).Options);
            }
            if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.MySql?.ConnectionString))
            {
                var mySqlConnectionString = globalSettings.MySql.ConnectionString;
                Options.Add(TestingDatabaseProviderOrder.MySql, new DbContextOptionsBuilder<DatabaseContext>().UseMySql(mySqlConnectionString, ServerVersion.AutoDetect(mySqlConnectionString)).Options);
            }
            if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.SqlServer?.ConnectionString))
            {
                Options.Add(TestingDatabaseProviderOrder.SqlServer, new DbContextOptionsBuilder<DatabaseContext>().UseSqlServer(globalSettings.SqlServer.ConnectionString).Options);
            }
        }
    }
}
