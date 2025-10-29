using AspNetCoreRateLimit;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.PushRegistration.Internal;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Services.Mail.Delivery;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NoopRepos = Bit.Core.Repositories.Noop;

#nullable enable

namespace Bit.IntegrationTestCommon.Factories;

public static class FactoryConstants
{
    public const string WhitelistedIp = "1.1.1.1";
}

public abstract class WebApplicationFactoryBase<T> : WebApplicationFactory<T>
    where T : class
{
    /// <summary>
    /// The database to use for this instance of the factory. By default it will use a shared database so all instances will connect to the same database during it's lifetime.
    /// </summary>
    /// <remarks>
    /// This will need to be set BEFORE using the <c>Server</c> property
    /// </remarks>
    public ITestDatabase TestDatabase { get; set; } = new SqliteTestDatabase();

    /// <summary>
    /// If set to <c>true</c> the factory will manage the database lifecycle, including migrations.
    /// </summary>
    /// <remarks>
    /// This will need to be set BEFORE using the <c>Server</c> property
    /// </remarks>
    public bool ManagesDatabase { get; set; } = true;

    private readonly List<Action<IServiceCollection>> _configureTestServices = new();
    private readonly List<Action<IConfigurationBuilder>> _configureAppConfiguration = new();

    public void SubstituteService<TService>(Action<TService> mockService)
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
    /// Allows you to add your own services to the application as required.
    /// </summary>
    /// <param name="configure">The service collection you want added to the test service collection.</param>
    /// <remarks>This needs to be ran BEFORE making any calls through the factory to take effect.</remarks>
    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        _configureTestServices.Add(configure);
    }

    /// <summary>
    /// Add your own configuration provider to the application.
    /// </summary>
    /// <param name="configure">The action adding your own providers.</param>
    /// <remarks>This needs to be ran BEFORE making any calls through the factory to take effect.</remarks>
    /// <example>
    ///   <code lang="C#">
    ///   factory.UpdateConfiguration(builder =&gt;
    ///   {
    ///       builder.AddInMemoryCollection(new Dictionary&lt;string, string?&gt;
    ///       {
    ///           { "globalSettings:attachment:connectionString", null},
    ///           { "globalSettings:events:connectionString", null},
    ///       })
    ///   })
    ///   </code>
    /// </example>
    public void UpdateConfiguration(Action<IConfigurationBuilder> configure)
    {
        _configureAppConfiguration.Add(configure);
    }

    /// <summary>
    /// Updates a single configuration entry for multiple entries at once use <see cref="UpdateConfiguration(Action{IConfigurationBuilder})"/>.
    /// </summary>
    /// <param name="key">The fully qualified name of the setting, using <c>:</c> as delimiter between sections.</param>
    /// <param name="value">The value of the setting.</param>
    /// <remarks>This needs to be ran BEFORE making any calls through the factory to take effect.</remarks>
    /// <example>
    ///   <code lang="C#">
    ///   factory.UpdateConfiguration("globalSettings:attachment:connectionString", null);
    ///   </code>
    /// </example>
    public void UpdateConfiguration(string key, string? value)
    {
        _configureAppConfiguration.Add(builder =>
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { key, value },
            });
        });
    }

    /// <summary>
    /// Configure the web host to use a SQLite in memory database
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var config = new Dictionary<string, string?>
        {
            // Manually insert a EF provider so that ConfigureServices will add EF repositories but we will override
            // DbContextOptions to use an in memory database
            { "globalSettings:databaseProvider", "postgres" },
            { "globalSettings:postgreSql:connectionString", "Host=localhost;Username=test;Password=test;Database=test" },

            // Clear the redis connection string for distributed caching, forcing an in-memory implementation
            { "globalSettings:redis:connectionString", "" },

            // Clear Storage
            { "globalSettings:attachment:connectionString", null },
            { "globalSettings:events:connectionString", null },
            { "globalSettings:send:connectionString", null },
            { "globalSettings:notifications:connectionString", null },
            { "globalSettings:storage:connectionString", null },

            // This will force it to use an ephemeral key for IdentityServer
            { "globalSettings:developmentDirectory", null },

            // Email Verification
            { "globalSettings:enableEmailVerification", "true" },
            { "globalSettings:disableUserRegistration", "false" },
            { "globalSettings:launchDarkly:flagValues:email-verification", "true" },

            // New Device Verification
            { "globalSettings:disableEmailNewDevice", "false" },

            // Web push notifications
            { "globalSettings:webPush:vapidPublicKey", "BGBtAM0bU3b5jsB14IjBYarvJZ6rWHilASLudTTYDDBi7a-3kebo24Yus_xYeOMZ863flAXhFAbkL6GVSrxgErg" },
        };

        // Some database drivers modify the connection string
        TestDatabase.ModifyGlobalSettings(config);

        builder.ConfigureAppConfiguration(c =>
        {
            c.SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json");

            c.AddUserSecrets(typeof(Identity.Startup).Assembly, optional: true);

            c.AddInMemoryCollection(config);
        });

        // Run configured actions after defaults to allow them to take precedence
        foreach (var configureAppConfiguration in _configureAppConfiguration)
        {
            builder.ConfigureAppConfiguration(configureAppConfiguration);
        }

        builder.ConfigureTestServices(services =>
        {
            var dbContextOptions =
                services.First(sd => sd.ServiceType == typeof(DbContextOptions<DatabaseContext>));
            services.Remove(dbContextOptions);

            // Add database to the service collection
            TestDatabase.AddDatabase(services);
            if (ManagesDatabase)
            {
                TestDatabase.Migrate(services);
            }

            // QUESTION: The normal licensing service should run fine on developer machines but not in CI
            // should we have a fork here to leave the normal service for developers?
            // TODO: Eventually add the license file to CI
            Replace<ILicensingService, NoopLicensingService>(services);

            // FUTURE CONSIDERATION: Add way to run this self hosted/cloud, for now it is cloud only
            Replace<IPushRegistrationService, NoopPushRegistrationService>(services);

            // Even though we are cloud we currently set this up as cloud, we can use the EF/selfhosted service
            // instead of using Noop for this service
            // TODO: Install and use azurite in CI pipeline
            Replace<IEventWriteService, RepositoryEventWriteService>(services);

            Replace<IEventRepository, EventRepository>(services);

            Replace<IMailDeliveryService, NoopMailDeliveryService>(services);

            // TODO: Install and use azurite in CI pipeline
            Replace<IInstallationDeviceRepository, NoopRepos.InstallationDeviceRepository>(services);

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

            // Noop StripePaymentService - this could be changed to integrate with our Stripe test account
            Replace(services, Substitute.For<IPaymentService>());

            Replace(services, Substitute.For<IOrganizationBillingService>());
        });

        foreach (var configureTestService in _configureTestServices)
        {
            builder.ConfigureTestServices(configureTestService);
        }
    }

    private static void Replace<TService, TNewImplementation>(IServiceCollection services)
        where TService : class
        where TNewImplementation : class, TService
    {
        services.RemoveAll<TService>();
        services.AddSingleton<TService, TNewImplementation>();
    }

    private static void Replace<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddSingleton<TService>(implementation);
    }

    public HttpClient CreateAuthedClient(string accessToken)
    {
        var handler = Server.CreateHandler((context) =>
        {
            context.Request.Headers.Authorization = $"Bearer {accessToken}";
        });

        return new HttpClient(handler)
        {
            BaseAddress = Server.BaseAddress,
            Timeout = TimeSpan.FromSeconds(200),
        };
    }

    public DatabaseContext GetDatabaseContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    }

    public TService GetService<TService>()
        where TService : notnull
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TService>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (ManagesDatabase)
        {
            // Avoid calling Dispose twice
            ManagesDatabase = false;
            TestDatabase.Dispose();
        }
    }
}
