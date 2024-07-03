using Bit.Core.Settings;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.DBSeeder;


    public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                throw new ArgumentException("Connection string must be provided as the first argument.");
            }

            var connectionString = args[0];

            var services = new ServiceCollection();
            services.AddDataProtection();
            services.AddDbContext<DatabaseContext>(options =>
                options.UseSqlServer(connectionString));
            var serviceProvider = services.BuildServiceProvider();

            return serviceProvider.GetRequiredService<DatabaseContext>();
        }
    }
