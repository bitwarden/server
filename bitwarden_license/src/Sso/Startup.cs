using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Bit.Sso.Utilities;
using IdentityServer4.Extensions;
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
        services.AddDistributedIdentityServices(globalSettings);
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
                ctx.SetIdentityServerOrigin($"{uri.Scheme}://{uri.Host}");
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
