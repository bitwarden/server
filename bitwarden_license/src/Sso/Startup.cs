﻿using Bit.Core;
using Bit.Core.Billing.Extensions;
using Bit.Core.Context;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.SecretsManager.Repositories.Noop;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Bit.Sso.Utilities;
using Duende.IdentityServer.Services;
using Microsoft.IdentityModel.Logging;
using Stripe;

namespace Bit.Sso;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
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

        // Stripe Billing
        StripeConfiguration.ApiKey = globalSettings.Stripe.ApiKey;
        StripeConfiguration.MaxNetworkRetries = globalSettings.Stripe.MaxNetworkRetries;

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Repositories
        services.AddDatabaseRepositories(globalSettings);

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();

        // Caching
        services.AddMemoryCache();
        services.AddDistributedCache(globalSettings);

        // Mvc
        services.AddControllersWithViews();

        // Cookies
        if (Environment.IsDevelopment())
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
                options.OnAppendCookie = ctx =>
                {
                    ctx.CookieOptions.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
                };
            });
        }

        // Authentication
        services.AddDistributedIdentityServices();
        services.AddAuthentication()
            .AddCookie(AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme);
        services.AddSsoServices(globalSettings);

        // IdentityServer
        services.AddSsoIdentityServerServices(Environment, globalSettings);

        // Identity
        services.AddCustomIdentityServices(globalSettings);

        // Services
        services.AddBaseServices(globalSettings);
        services.AddDefaultServices(globalSettings);
        services.AddCoreLocalizationServices();
        services.AddBillingOperations();

        // TODO: Remove when OrganizationUser methods are moved out of OrganizationService, this noop dependency should
        // TODO: no longer be required - see PM-1880
        services.AddScoped<IServiceAccountRepository, NoopServiceAccountRepository>();

        // This should be registered last because it customizes the primary http message handler and we want it to win.
        services.AddX509ChainCustomization();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings,
        ILogger<Startup> logger)
    {
        if (env.IsDevelopment() || globalSettings.SelfHosted)
        {
            IdentityModelEventSource.ShowPII = true;
        }

        app.UseSerilog(env, appLifetime, globalSettings);

        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (!env.IsDevelopment())
        {
            var uri = new Uri(globalSettings.BaseServiceUri.Sso);
            app.Use(async (ctx, next) =>
            {
                ctx.RequestServices.GetRequiredService<IServerUrls>().Origin = $"{uri.Scheme}://{uri.Host}";
                await next();
            });
        }

        if (globalSettings.SelfHosted)
        {
            app.UsePathBase("/sso");
            app.UseForwardedHeaders(globalSettings);
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseCookiePolicy();
        }
        else
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseCoreLocalization();

        // Add static files to the request pipeline.
        app.UseStaticFiles();

        // Add routing
        app.UseRouting();

        // Add Cors
        app.UseCors(policy => policy.SetIsOriginAllowed(o => CoreHelpers.IsCorsOriginAllowed(o, globalSettings))
            .AllowAnyMethod().AllowAnyHeader().AllowCredentials());

        // Add current context
        app.UseMiddleware<CurrentContextMiddleware>();

        // Add IdentityServer to the request pipeline.
        app.UseIdentityServer(new IdentityServerMiddlewareOptions
        {
            AuthenticationMiddleware = app => app.UseMiddleware<SsoAuthenticationMiddleware>()
        });

        // Add Mvc stuff
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());

        // Log startup
        logger.LogInformation(Constants.BypassFiltersEventId, globalSettings.ProjectName + " started.");
    }
}
