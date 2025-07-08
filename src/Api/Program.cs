﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AspNetCoreRateLimit;
using Bit.Core.Utilities;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Api;

public class Program
{
    public static void Main(string[] args)
    {
        Host
            .CreateDefaultBuilder(args)
            .ConfigureCustomAppConfiguration(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.ConfigureLogging((hostingContext, logging) =>
                    logging.AddSerilog(hostingContext, (e, globalSettings) =>
                    {
                        var context = e.Properties["SourceContext"].ToString();
                        if (e.Exception != null &&
                            (e.Exception.GetType() == typeof(SecurityTokenValidationException) ||
                                e.Exception.Message == "Bad security stamp."))
                        {
                            return false;
                        }

                        if (
                            context.Contains(typeof(IpRateLimitMiddleware).FullName))
                        {
                            return e.Level >= globalSettings.MinLogLevel.ApiSettings.IpRateLimit;
                        }

                        if (context.Contains("Duende.IdentityServer.Validation.TokenValidator") ||
                            context.Contains("Duende.IdentityServer.Validation.TokenRequestValidator"))
                        {
                            return e.Level >= globalSettings.MinLogLevel.ApiSettings.IdentityToken;
                        }

                        return e.Level >= globalSettings.MinLogLevel.ApiSettings.Default;
                    }));
            })
            .Build()
            .Run();
    }
}
