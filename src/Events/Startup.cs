using System.Globalization;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.IdentityServer;
using Bit.Core.Services;
using Bit.Core.Services.Implementations;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Duende.IdentityModel;

namespace Bit.Events;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
        Environment = env;
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment Environment { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Repositories
        services.AddDatabaseRepositories(globalSettings);

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();

        // Identity
        services.AddIdentityAuthenticationServices(globalSettings, Environment, config =>
        {
            config.AddPolicy("Application", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.AuthenticationMethod, "Application", "external");
                policy.RequireClaim(JwtClaimTypes.Scope, ApiScopes.Api);
            });
        });

        // Services
        var usingServiceBusAppCache = CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ConnectionString) &&
            CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ApplicationCacheTopicName);
        services.AddScoped<IApplicationCacheService, FeatureRoutedCacheService>();
        services.AddSingleton<IVNextInMemoryApplicationCacheService, VNextInMemoryApplicationCacheService>();

        if (usingServiceBusAppCache)
        {
            services.AddScoped<IApplicationCacheBackwardProcessor, FeatureRoutedCacheService>();
            services.AddSingleton<IVCurrentInMemoryApplicationCacheService, InMemoryServiceBusApplicationCacheService>();
            services.AddSingleton<IApplicationCacheServiceBusMessaging, ServiceBusApplicationCacheMessaging>();
        }
        else
        {
            services.AddSingleton<IVCurrentInMemoryApplicationCacheService, InMemoryApplicationCacheService>();
            services.AddSingleton<IApplicationCacheServiceBusMessaging, NoOpApplicationCacheMessaging>();
        }

        services.AddEventWriteServices(globalSettings);
        services.AddScoped<IEventService, EventService>();

        services.AddOptionality();

        // Mvc
        services.AddMvc(config =>
        {
            config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
        });

        if (usingServiceBusAppCache)
        {
            services.AddHostedService<Core.HostedServices.ApplicationCacheHostedService>();
        }

        services.AddRabbitMqListeners(globalSettings);
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings)
    {
        app.UseSerilog(env, appLifetime, globalSettings);

        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // Forwarding Headers
        if (globalSettings.SelfHosted)
        {
            app.UseForwardedHeaders(globalSettings);
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Default Middleware
        app.UseDefaultMiddleware(env, globalSettings);

        // Add routing
        app.UseRouting();

        // Add Cors
        app.UseCors(policy => policy.SetIsOriginAllowed(o => CoreHelpers.IsCorsOriginAllowed(o, globalSettings))
            .AllowAnyMethod().AllowAnyHeader().AllowCredentials());

        // Add authentication and authorization to the request pipeline.
        app.UseAuthentication();
        app.UseAuthorization();

        // Add current context
        app.UseMiddleware<CurrentContextMiddleware>();

        // Add MVC to the request pipeline.
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
