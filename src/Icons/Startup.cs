using System.Globalization;
using Bit.Icons.Extensions;
using Bit.Icons.Models;
using Bitwarden.Server.Sdk.Environment;
using Microsoft.Net.Http.Headers;

namespace Bit.Icons;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        services.AddGlobalSettingsBridge();
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
        services.AddCaches(iconsSettings, changePasswordUriSettings);

        // Services
        services.AddServices();

        // Mvc
        services.AddMvc();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IBitwardenEnvironment environment)
    {
        // Add general security headers
        app.UseSecurityHeaders();

        // Forwarding Headers
        if (environment.SelfHosted)
        {
            app.UseForwardedHeaders();
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.Use(async (context, next) =>
        {
            // Static icon assets are cacheable long-term. The change-password endpoint sets its own
            // Cache-Control per result (see ChangePasswordUriController), so skip it here.
            if (!context.Request.Path.StartsWithSegments("/change-password-uri"))
            {
                context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromDays(7)
                };
            }

            context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'none'");

            await next();
        });

        app.UseCors();

        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultControllerRoute();
            endpoints.MapVersionEndpoint();
        });
    }
}
