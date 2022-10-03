using AspNetCoreRateLimit;
using Bit.Core.Utilities;

namespace Bit.Identity;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args)
            .Build()
            .Run();
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

                        if (context.Contains("IdentityServer4.Validation.TokenValidator") ||
                            context.Contains("IdentityServer4.Validation.TokenRequestValidator"))
                        {
                            return e.Level >= globalSettings.MinLogLevel.IdentitySettings.IdentityToken;
                        }

                        return e.Level >= globalSettings.MinLogLevel.IdentitySettings.Default;
                    }));
            });
    }
}
