using System;
using System.Collections.Generic;
using Bit.Core.Enums;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MySqlMigrations
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

    public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
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
                b => b.MigrationsAssembly("MySqlMigrations"));
            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}
