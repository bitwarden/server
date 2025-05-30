using Bit.Core.Settings;
using Bit.SharedWeb.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Bit.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        var startup = new Startup(builder.Environment, builder.Configuration);

        startup.ConfigureServices(builder.Services);

        var app = builder.Build();

        app.MapDefaultEndpoints();

        var globalSettings = app.Services.GetRequiredService<GlobalSettings>();
        var logger = app.Services.GetRequiredService<ILogger<Startup>>();

        startup.Configure(app, app.Environment, app.Lifetime, globalSettings, logger);

        app.MapDefaultControllerRoute();

        if (!globalSettings.SelfHosted)
        {
            app.MapHealthChecks("/healthz");

            app.MapHealthChecks("/healthz/extended", new HealthCheckOptions
            {
                ResponseWriter = HealthCheckServiceExtensions.WriteResponse
            });
        }

        app.Run();

        // Host
        //     .CreateDefaultBuilder(args)
        //     .ConfigureCustomAppConfiguration(args)
        //     .ConfigureWebHostDefaults(webBuilder =>
        //     {
        //         webBuilder.UseStartup<Startup>();
        //         webBuilder.ConfigureLogging((hostingContext, logging) =>
        //             logging.AddSerilog(hostingContext, (e, globalSettings) =>
        //             {
        //                 var context = e.Properties["SourceContext"].ToString();
        //                 if (e.Exception != null &&
        //                     (e.Exception.GetType() == typeof(SecurityTokenValidationException) ||
        //                         e.Exception.Message == "Bad security stamp."))
        //                 {
        //                     return false;
        //                 }

        //                 if (
        //                     context.Contains(typeof(IpRateLimitMiddleware).FullName))
        //                 {
        //                     return e.Level >= globalSettings.MinLogLevel.ApiSettings.IpRateLimit;
        //                 }

        //                 if (context.Contains("Duende.IdentityServer.Validation.TokenValidator") ||
        //                     context.Contains("Duende.IdentityServer.Validation.TokenRequestValidator"))
        //                 {
        //                     return e.Level >= globalSettings.MinLogLevel.ApiSettings.IdentityToken;
        //                 }

        //                 return e.Level >= globalSettings.MinLogLevel.ApiSettings.Default;
        //             }));
        //     })
        //     .Build()
        //     .Run();
    }
}
