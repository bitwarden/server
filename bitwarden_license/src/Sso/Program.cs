﻿using Bit.Core.Utilities;
using Serilog;

namespace Bit.Sso;

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
                    if (e.Properties.TryGetValue("RequestPath", out var requestPath) &&
                        !string.IsNullOrWhiteSpace(requestPath?.ToString()) &&
                        (context.Contains(".Server.Kestrel") || context.Contains(".Core.IISHttpServer")))
                    {
                        return false;
                    }
                    return e.Level >= globalSettings.MinLogLevel.SsoSettings.Default;
                }));
            })
            .Build()
            .Run();
    }
}
