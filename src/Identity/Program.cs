using AspNetCoreRateLimit;
using Bit.Core.Settings;
using Bit.Core.Utilities;

namespace Bit.Identity;

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

        var settings = app.Services.GetRequiredService<GlobalSettings>();
        var logger = app.Services.GetRequiredService<ILogger<Startup>>();

        startup.Configure(app, app.Environment, app.Lifetime, settings, logger);

        app.Run();

        // CreateHostBuilder(args)
        //     .Build()
        //     .Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host
            .CreateDefaultBuilder(args)
            .ConfigureCustomAppConfiguration(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.ConfigureLogging((hostingContext, logging) =>
                    logging.AddSerilog(hostingContext, (e, globalSettings) =>
                    {
                        var context = e.Properties["SourceContext"].ToString();
                        if (context.Contains(typeof(IpRateLimitMiddleware).FullName))
                        {
                            return e.Level >= globalSettings.MinLogLevel.IdentitySettings.IpRateLimit;
                        }

                        if (context.Contains("Duende.IdentityServer.Validation.TokenValidator") ||
                            context.Contains("Duende.IdentityServer.Validation.TokenRequestValidator"))
                        {
                            return e.Level >= globalSettings.MinLogLevel.IdentitySettings.IdentityToken;
                        }

                        return e.Level >= globalSettings.MinLogLevel.IdentitySettings.Default;
                    }));
            });
    }
}
