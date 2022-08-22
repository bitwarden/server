using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Bit.Core.Test.Helpers.Factories
{
    public static class DatabaseOptionsFactory
    {
        public static List<DbContextOptions<DatabaseContext>> Options { get; } = new();

        static DatabaseOptionsFactory()
        {
            var globalSettings = GlobalSettingsFactory.GlobalSettings;
            if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.PostgreSql?.ConnectionString))
            {
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                Options.Add(new DbContextOptionsBuilder<DatabaseContext>().UseNpgsql(globalSettings.PostgreSql.ConnectionString).Options);
            }
            if (!string.IsNullOrWhiteSpace(GlobalSettingsFactory.GlobalSettings.MySql?.ConnectionString))
            {
                var mySqlConnectionString = globalSettings.MySql.ConnectionString;
                Options.Add(new DbContextOptionsBuilder<DatabaseContext>().UseMySql(mySqlConnectionString, ServerVersion.AutoDetect(mySqlConnectionString)).Options);
            }
        }
    }

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
}
