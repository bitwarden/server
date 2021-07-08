using System.Collections.Generic;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Bit.Core.Enums;
using Microsoft.EntityFrameworkCore.Design;
using System;

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

            var selectedDatabaseProvider = globalSettings.DatabaseProvider;
            var provider = SupportedDatabaseProviders.Postgres;
            var connectionString = string.Empty;
            if (!string.IsNullOrWhiteSpace(selectedDatabaseProvider))
            {
                switch (selectedDatabaseProvider.ToLowerInvariant())
                {
                    case "postgres":
                    case "postgresql":
                        provider = SupportedDatabaseProviders.Postgres;
                        connectionString = globalSettings.PostgreSql.ConnectionString;
                        break;
                    case "mysql":
                    case "mariadb":
                        provider = SupportedDatabaseProviders.MySql;
                        connectionString = globalSettings.MySql.ConnectionString;
                        break;
                    default:
                        throw new Exception("No database provider selected");
                        break;
                }
            }
            if (provider.Equals(SupportedDatabaseProviders.Postgres))
            {
                optionsBuilder.UseNpgsql(
                    connectionString,  
                    b => b.MigrationsAssembly("PostgresMigrations"));
            }
            else if (provider.Equals(SupportedDatabaseProviders.MySql))
            {
                optionsBuilder.UseMySql(
                    connectionString, 
                    ServerVersion.AutoDetect(connectionString),
                    b => b.MigrationsAssembly("MySqlMigrations"));
            }
            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}
