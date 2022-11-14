using System.Globalization;
using Bit.Core.Context;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using IdentityModel;

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
                policy.RequireClaim(JwtClaimTypes.Scope, "api");
            });
        });

        // Services
        var usingServiceBusAppCache = CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ConnectionString) &&
            CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ApplicationCacheTopicName);
        if (usingServiceBusAppCache)
        {
            services.AddSingleton<IApplicationCacheService, InMemoryServiceBusApplicationCacheService>();
        }
        else
        {
            services.AddSingleton<IApplicationCacheService, InMemoryApplicationCacheService>();
        }
        services.AddScoped<IEventService, EventService>();
        if (!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Events.ConnectionString))
        {
            services.AddSingleton<IEventWriteService, AzureQueueEventWriteService>();
        }
        else
        {
            services.AddSingleton<IEventWriteService, RepositoryEventWriteService>();
        }

        // Mvc
        services.AddMvc(config =>
        {
            config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
        });

        if (usingServiceBusAppCache)
        {
            services.AddHostedService<Core.HostedServices.ApplicationCacheHostedService>();
        }
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
