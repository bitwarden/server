﻿using System.Globalization;
using Bit.Core.Billing.Extensions;
using Bit.Core.Context;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.SecretsManager.Repositories.Noop;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Scim.Context;
using Bit.Scim.Utilities;
using Bit.SharedWeb.Utilities;
using IdentityModel;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stripe;

namespace Bit.Scim;

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
        services.Configure<ScimSettings>(Configuration.GetSection("ScimSettings"));

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Stripe Billing
        StripeConfiguration.ApiKey = globalSettings.Stripe.ApiKey;
        StripeConfiguration.MaxNetworkRetries = globalSettings.Stripe.MaxNetworkRetries;

        // Repositories
        services.AddDatabaseRepositories(globalSettings);

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();
        services.AddScoped<IScimContext, ScimContext>();

        // Authentication
        services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.DefaultScheme, null);

        services.AddAuthorization(config =>
        {
            config.AddPolicy("Scim", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(JwtClaimTypes.Scope, "api.scim");
            });
        });

        // Identity
        services.AddCustomIdentityServices(globalSettings);

        // Services
        services.AddBaseServices(globalSettings);
        services.AddDefaultServices(globalSettings);
        services.AddDistributedCache(globalSettings);
        services.AddBillingOperations();

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // TODO: Remove when OrganizationUser methods are moved out of OrganizationService, this noop dependency should
        // TODO: no longer be required - see PM-1880
        services.AddScoped<IServiceAccountRepository, NoopServiceAccountRepository>();

        // Mvc
        services.AddMvc(config =>
        {
            config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
        });
        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        services.AddScimGroupCommands();
        services.AddScimGroupQueries();
        services.AddScimUserQueries();
        services.AddScimUserCommands();
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

        // Add Scim context
        app.UseMiddleware<ScimContextMiddleware>();

        // Add authentication and authorization to the request pipeline.
        app.UseAuthentication();
        app.UseAuthorization();

        // Add current context
        app.UseMiddleware<CurrentContextMiddleware>();

        // Add MVC to the request pipeline.
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
