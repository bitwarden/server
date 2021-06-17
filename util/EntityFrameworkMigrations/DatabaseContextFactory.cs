using System;
using System.Configuration;
using System.Linq;
using Bit.Core.Models.EntityFramework;
using Bit.Core.Models.EntityFramework.Provider;
using Bit.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Enums;

namespace EntityFrameworkMigrations
{
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
                    b => b.MigrationsAssembly("EntityFrameworkMigrations"));
            }
            else if (provider.Equals(SupportedDatabaseProviders.MySql))
            {
                optionsBuilder.UseMySql(
                    connectionString, 
                    ServerVersion.AutoDetect(connectionString),
                    b => b.MigrationsAssembly("EntityFrameworkMigrations"));
            }
            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}
