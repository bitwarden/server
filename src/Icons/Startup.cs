using System.Globalization;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Icons.Extensions;
using Bit.Icons.Models;
using Bit.SharedWeb.Utilities;
using Microsoft.Net.Http.Headers;

namespace Bit.Icons;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
        Environment = env;
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment Environment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);
        var iconsSettings = new IconsSettings();
        var changePasswordUriSettings = new ChangePasswordUriSettings();
        ConfigurationBinder.Bind(Configuration.GetSection("IconsSettings"), iconsSettings);
        ConfigurationBinder.Bind(Configuration.GetSection("ChangePasswordUriSettings"), changePasswordUriSettings);
        services.AddSingleton(s => iconsSettings);
        services.AddSingleton(s => changePasswordUriSettings);

        // Http client
        services.ConfigureHttpClients();

        // Add HtmlParser
        services.AddHtmlParsing();

        // Cache
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = iconsSettings.CacheSizeLimit;
        });
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = changePasswordUriSettings.CacheSizeLimit;
        });

        // Services
        services.AddServices();

        // Mvc
        services.AddMvc();
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

        app.Use(async (context, next) =>
        {
            context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(7)
            };

            context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'none'");

            await next();
        });

        app.UseCors(policy => policy.SetIsOriginAllowed(o => CoreHelpers.IsCorsOriginAllowed(o, globalSettings))
            .AllowAnyMethod().AllowAnyHeader().AllowCredentials());

        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
