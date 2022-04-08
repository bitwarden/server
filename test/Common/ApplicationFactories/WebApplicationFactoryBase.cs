using System.Collections.Generic;
using System.Linq;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Test.Common.ApplicationFactories
{
    public abstract class WebApplicationFactoryBase<T> : WebApplicationFactory<T>
        where T : class
    {
        /// <summary>
        /// Configure the web host to use an EF in memory database
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(c =>
            {
                c.AddInMemoryCollection(new List<KeyValuePair<string, string>>()
                {
                    // Manually insert a EF provider so that ConfigureServices will add EF repositories but we will override
                    // DbContextOptions to use an in memory database
                    new KeyValuePair<string, string>("globalSettings:databaseProvider", "postgres"),
                    new KeyValuePair<string, string>("globalSettings:postgreSql:connectionString", "Host=localhost;Username=test;Password=test;Database=test"),
                });
            });

            builder.ConfigureTestServices(services =>
            {
                var dbContextOptions = services.First(sd => sd.ServiceType == typeof(DbContextOptions<DatabaseContext>));
                services.Remove(dbContextOptions);
                services.AddScoped(_ =>
                {
                    return new DbContextOptionsBuilder<DatabaseContext>()
                        .UseInMemoryDatabase("test_database")
                        .Options;
                });

                // QUESTION: The normal licensing service should run fine on developer machines but not in CI
                // should we have a fork here to leave the normal service for developers?
                var licensingService = services.First(sd => sd.ServiceType == typeof(ILicensingService));
                services.Remove(licensingService);
                services.AddSingleton<ILicensingService, NoopLicensingService>();

                // QUESTION: Should these unit tests instead run as self hosted installs so this is done automatically?
                var pushRegistrationService = services.First(sd => sd.ServiceType == typeof(IPushRegistrationService));
                services.Remove(pushRegistrationService);
                services.AddSingleton<IPushRegistrationService, NoopPushRegistrationService>();
            });
        }

        public DatabaseContext GetDatabaseContext()
        {
            var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        }
    }
}
