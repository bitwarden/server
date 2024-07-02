using System;
using CommandDotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Bit.Core.Entities;

namespace EFDBSeederUtility
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return new AppRunner<Program>().Run(args);
        }

        [Command(Description = "Seed the database")]
        public void Seed(
            [Option(Description = "Database provider (mssql, mysql, postgres, sqlite).")] string databaseProvider,
            [Option(Description = "Database connection string.")] string connectionString)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, databaseProvider, connectionString);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            SeedData.Initialize(serviceProvider);
        }

        private static void ConfigureServices(IServiceCollection services, string databaseProvider, string connectionString)
        {
            switch (databaseProvider.ToLower())
            {
                case "mssql":
                    services.AddDbContext<DatabaseContext>(options =>
                        options.UseSqlServer(connectionString));
                    break;
                case "mysql":
                    services.AddDbContext<DatabaseContext>(options =>
                        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
                    break;
                case "postgres":
                    services.AddDbContext<DatabaseContext>(options =>
                        options.UseNpgsql(connectionString));
                    break;
                case "sqlite":
                    services.AddDbContext<DatabaseContext>(options =>
                        options.UseSqlite(connectionString));
                    break;
                default:
                    throw new ArgumentException("Unsupported database provider.");
            }
        }
    }
}
