using System.Net.Http.Headers;
using AspNetCoreRateLimit;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

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
    public string DatabaseName { get; set; } = FactoryConstants.DefaultDatabaseName;

    /// <summary>
    /// Tell the server to startup in self hosted mode and self hosted specific configuration
    /// </summary>
    public bool SelfHosted { get; set; }


    private readonly List<Action<WebHostBuilderContext, IConfigurationBuilder>> _configureAppConfigurationActions = new();

    public void AddConfiguration(string key, string value)
    {
        _configureAppConfigurationActions.Add((_, builder) =>
        {
            builder.AddInMemoryCollection(new Dictionary<string, string>
            {
                { key, value },
            });
        });
    }

    private readonly List<Action<IServiceCollection>> _configureTestServiceActions = new();

    public void SubstituteService<TService>(Action<TService> configureService = null)
        where TService : class
    {
        _configureTestServiceActions.Add(services =>
        {
            var existingService = services.FirstOrDefault(sd => sd.ServiceType == typeof(TService));
            if (existingService != null)
            {
                services.Remove(existingService);
            }

            var substitutedService = Substitute.For<TService>();
            configureService?.Invoke(substitutedService);
            services.AddSingleton(substitutedService);
        });
    }

    public void OverrideHttpHandler(string httpClientName, HttpMessageHandler handler)
    {
        _configureTestServiceActions.Add(services =>
        {
            services.AddHttpClient(httpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
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
                { "globalSettings:selfHosted", SelfHosted ? "true" : "false" },
                // Manually insert a EF provider so that ConfigureServices will add EF repositories but we will override
                // DbContextOptions to use an in memory database
                { "globalSettings:databaseProvider", "postgres" },
                { "globalSettings:postgreSql:connectionString", "Host=localhost;Username=test;Password=test;Database=test" },

                // Clear the redis connection string for distributed caching, forcing an in-memory implementation
                { "globalSettings:redis:connectionString", ""}
            });
        });

        foreach (var appConfigurationAction in _configureAppConfigurationActions)
        {
            builder.ConfigureAppConfiguration(appConfigurationAction);
        }

        builder.ConfigureTestServices(services =>
        {
            var dbContextOptions = services.First(sd => sd.ServiceType == typeof(DbContextOptions<DatabaseContext>));
            services.Remove(dbContextOptions);
            services.AddScoped(_ =>
            {
                return new DbContextOptionsBuilder<DatabaseContext>()
                    .UseInMemoryDatabase(DatabaseName)
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
        });

        foreach (var configureTestServicesAction in _configureTestServiceActions)
        {
            builder.ConfigureTestServices(configureTestServicesAction);
        }
    }

    public DatabaseContext GetDatabaseContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    }

    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
