using System.Collections.Generic;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Bit.Core.Test.Helpers.Factories
{
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

    public static class DatabaseOptionsFactory
    {
        public static List<DbContextOptions<DatabaseContext>> Options { get; } = new List<DbContextOptions<DatabaseContext>>();

        static DatabaseOptionsFactory()
        {
            Options.Add(new DbContextOptionsBuilder<DatabaseContext>().UseNpgsql(GlobalSettingsFactory.GlobalSettings.PostgreSql.ConnectionString).Options);
        }
    }
}
