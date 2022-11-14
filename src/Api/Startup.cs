using Bit.Api.Utilities;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Identity;
using Bit.Core.Settings;
using AspNetCoreRateLimit;
using Stripe;
using Bit.Core.Utilities;
using IdentityModel;
using System.Globalization;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi.Models;
using Bit.SharedWeb.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;

#if !OSS
using Bit.Commercial.Core.Utilities;
#endif

namespace Bit.Api;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
        Environment = env;
    }

    public IConfiguration Configuration { get; private set; }
    public IWebHostEnvironment Environment { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);
        if (!globalSettings.SelfHosted)
        {
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimitOptions"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
        }

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Event Grid
        if (!string.IsNullOrWhiteSpace(globalSettings.EventGridKey))
        {
            ApiHelpers.EventGridKey = globalSettings.EventGridKey;
        }

        // Stripe Billing
        StripeConfiguration.ApiKey = globalSettings.Stripe.ApiKey;
        StripeConfiguration.MaxNetworkRetries = globalSettings.Stripe.MaxNetworkRetries;

        // Repositories
        services.AddDatabaseRepositories(globalSettings);

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Caching
        services.AddMemoryCache();
        services.AddDistributedCache(globalSettings);

        // BitPay
        services.AddSingleton<BitPayClient>();

        if (!globalSettings.SelfHosted)
        {
            services.AddIpRateLimiting(globalSettings);
        }

        // Identity
        services.AddCustomIdentityServices(globalSettings);
        services.AddIdentityAuthenticationServices(globalSettings, Environment, config =>
        {
            config.AddPolicy("Application", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.AuthenticationMethod, "Application", "external");
                policy.RequireClaim(JwtClaimTypes.Scope, "api");
            });
            config.AddPolicy("Web", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.AuthenticationMethod, "Application", "external");
                policy.RequireClaim(JwtClaimTypes.Scope, "api");
                policy.RequireClaim(JwtClaimTypes.ClientId, "web");
            });
            config.AddPolicy("Push", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.Scope, "api.push");
            });
            config.AddPolicy("Licensing", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.Scope, "api.licensing");
            });
            config.AddPolicy("Organization", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.Scope, "api.organization");
            });
            config.AddPolicy("Installation", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.Scope, "api.installation");
            });
        });

        services.AddScoped<AuthenticatorTokenProvider>();

        // Services
        services.AddBaseServices(globalSettings);
        services.AddDefaultServices(globalSettings);
        services.AddCoreLocalizationServices();

#if OSS
        services.AddOosServices();
#else
        services.AddCommCoreServices();
#endif

        // MVC
        services.AddMvc(config =>
        {
            config.Conventions.Add(new ApiExplorerGroupConvention());
            config.Conventions.Add(new PublicApiControllersModelConvention());
        });

        services.AddSwagger(globalSettings);
        Jobs.JobsHostedService.AddJobsServices(services, globalSettings.SelfHosted);
        services.AddHostedService<Jobs.JobsHostedService>();

        if (CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ConnectionString) &&
            CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ApplicationCacheTopicName))
        {
            services.AddHostedService<Core.HostedServices.ApplicationCacheHostedService>();
        }
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings,
        ILogger<Startup> logger)
    {
        IdentityModelEventSource.ShowPII = true;
        app.UseSerilog(env, appLifetime, globalSettings);

        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

        // Default Middleware
        app.UseDefaultMiddleware(env, globalSettings);

        if (!globalSettings.SelfHosted)
        {
            // Rate limiting
            app.UseMiddleware<CustomIpRateLimitMiddleware>();
        }
        else
        {
            app.UseForwardedHeaders(globalSettings);
        }

        // Add localization
        app.UseCoreLocalization();

        // Add static files to the request pipeline.
        app.UseStaticFiles();

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

        // Add endpoints to the request pipeline.
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());

        // Add Swagger
        if (Environment.IsDevelopment() || globalSettings.SelfHosted)
        {
            app.UseSwagger(config =>
            {
                config.RouteTemplate = "specs/{documentName}/swagger.json";
                config.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                    swaggerDoc.Servers = new List<OpenApiServer>
                    {
                        new OpenApiServer { Url = globalSettings.BaseServiceUri.Api }
                    });
            });
            app.UseSwaggerUI(config =>
            {
                config.DocumentTitle = "Bitwarden API Documentation";
                config.RoutePrefix = "docs";
                config.SwaggerEndpoint($"{globalSettings.BaseServiceUri.Api}/specs/public/swagger.json",
                    "Bitwarden Public API");
                config.OAuthClientId("accountType.id");
                config.OAuthClientSecret("secretKey");
            });
        }

        // Log startup
        logger.LogInformation(Constants.BypassFiltersEventId, globalSettings.ProjectName + " started.");
    }
}
