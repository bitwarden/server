using AspNetCoreRateLimit;
using Bit.Core.Auth.Services;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NoopRepos = Bit.Core.Repositories.Noop;

namespace Bit.IntegrationTestCommon.Factories;

public static class FactoryConstants
{
    public const string DefaultDatabaseName = "test_database";
    public const string WhitelistedIp = "1.1.1.1";
}

public abstract class WebApplicationFactoryBase<T> : WebApplicationFactory<T>
    where T : class
{
    /// <summary>
    /// The database name to use for this instance of the factory. By default it will use a shared database name so all instances will connect to the same database during it's lifetime.
    /// </summary>
    /// <remarks>
    /// This will need to be set BEFORE using the <c>Server</c> property
    /// </remarks>
    public string DatabaseName { get; set; } = Guid.NewGuid().ToString();

    private readonly List<Action<IServiceCollection>> _configureTestServices = new();

    public void SubstitueService<TService>(Action<TService> mockService)
        where TService : class
    {
        _configureTestServices.Add(services =>
        {
            var foundServiceDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(TService))
                ?? throw new InvalidOperationException($"Could not find service of type {typeof(TService).FullName} to substitute");
            services.Remove(foundServiceDescriptor);

            var substitutedService = Substitute.For<TService>();
            mockService(substitutedService);
            services.Add(ServiceDescriptor.Singleton(typeof(TService), substitutedService));
        });
    }

    /// <summary>
    /// Configure the web host to use an EF in memory database
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(c =>
        {
            c.SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json");

            c.AddUserSecrets(typeof(Identity.Startup).Assembly, optional: true);
            c.AddInMemoryCollection(new Dictionary<string, string>
            {
                // Manually insert a EF provider so that ConfigureServices will add EF repositories but we will override
                // DbContextOptions to use an in memory database
                { "globalSettings:databaseProvider", "postgres" },
                { "globalSettings:postgreSql:connectionString", "Host=localhost;Username=test;Password=test;Database=test" },

                // Clear the redis connection string for distributed caching, forcing an in-memory implementation
                { "globalSettings:redis:connectionString", ""},

                // Clear Storage
                { "globalSettings:attachment:connectionString", null},
                { "globalSettings:events:connectionString", null},
                { "globalSettings:send:connectionString", null},
                { "globalSettings:notifications:connectionString", null},
                { "globalSettings:storage:connectionString", null},
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var dbContextOptions = services.First(sd => sd.ServiceType == typeof(DbContextOptions<DatabaseContext>));
            services.Remove(dbContextOptions);
            services.AddScoped(services =>
            {
                return new DbContextOptionsBuilder<DatabaseContext>()
                    .UseInMemoryDatabase(DatabaseName)
                    .UseApplicationServiceProvider(services)
                    .Options;
            });

            // QUESTION: The normal licensing service should run fine on developer machines but not in CI
            // should we have a fork here to leave the normal service for developers?
            // TODO: Eventually add the license file to CI
            var licensingService = services.First(sd => sd.ServiceType == typeof(ILicensingService));
            services.Remove(licensingService);
            services.AddSingleton<ILicensingService, NoopLicensingService>();

            // FUTURE CONSIDERATION: Add way to run this self hosted/cloud, for now it is cloud only
            var pushRegistrationService = services.First(sd => sd.ServiceType == typeof(IPushRegistrationService));
            services.Remove(pushRegistrationService);
            services.AddSingleton<IPushRegistrationService, NoopPushRegistrationService>();

            // Even though we are cloud we currently set this up as cloud, we can use the EF/selfhosted service
            // instead of using Noop for this service
            // TODO: Install and use azurite in CI pipeline
            var eventWriteService = services.First(sd => sd.ServiceType == typeof(IEventWriteService));
            services.Remove(eventWriteService);
            services.AddSingleton<IEventWriteService, RepositoryEventWriteService>();

            var eventRepositoryService = services.First(sd => sd.ServiceType == typeof(IEventRepository));
            services.Remove(eventRepositoryService);
            services.AddSingleton<IEventRepository, EventRepository>();

            var mailDeliveryService = services.First(sd => sd.ServiceType == typeof(IMailDeliveryService));
            services.Remove(mailDeliveryService);
            services.AddSingleton<IMailDeliveryService, NoopMailDeliveryService>();

            var captchaValidationService = services.First(sd => sd.ServiceType == typeof(ICaptchaValidationService));
            services.Remove(captchaValidationService);
            services.AddSingleton<ICaptchaValidationService, NoopCaptchaValidationService>();

            // Disable blocking
            var blockingService = services.First(sd => sd.ServiceType == typeof(IBlockIpService));
            services.Remove(blockingService);
            services.AddSingleton<IBlockIpService, NoopBlockIpService>();

            // TODO: Install and use azurite in CI pipeline
            var installationDeviceRepository =
                services.First(sd => sd.ServiceType == typeof(IInstallationDeviceRepository));
            services.Remove(installationDeviceRepository);
            services.AddSingleton<IInstallationDeviceRepository, NoopRepos.InstallationDeviceRepository>();

            // TODO: Install and use azurite in CI pipeline
            var metaDataRepository =
                services.First(sd => sd.ServiceType == typeof(IMetaDataRepository));
            services.Remove(metaDataRepository);
            services.AddSingleton<IMetaDataRepository, NoopRepos.MetaDataRepository>();

            // TODO: Install and use azurite in CI pipeline
            var referenceEventService = services.First(sd => sd.ServiceType == typeof(IReferenceEventService));
            services.Remove(referenceEventService);
            services.AddSingleton<IReferenceEventService, NoopReferenceEventService>();

            // Our Rate limiter works so well that it begins to fail tests unless we carve out
            // one whitelisted ip. We should still test the rate limiter though and they should change the Ip
            // to something that is NOT whitelisted
            services.Configure<IpRateLimitOptions>(options =>
            {
                options.IpWhitelist = new List<string>
                {
                    FactoryConstants.WhitelistedIp,
                };
            });

            // Fix IP Rate Limiting
            services.AddSingleton<IStartupFilter, CustomStartupFilter>();

            // Disable logs
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        });

        foreach (var configureTestService in _configureTestServices)
        {
            builder.ConfigureTestServices(configureTestService);
        }
    }

    public DatabaseContext GetDatabaseContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    }

    public TS GetService<TS>()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TS>();
    }
}
