using Bit.Core.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog.Events;

namespace Bit.Admin
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Host
                .CreateDefaultBuilder(args)
                .ConfigureCustomAppConfiguration(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(o =>
                    {
                        o.Limits.MaxRequestLineSize = 20_000;
                    });
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureLogging((hostingContext, logging) =>
                    logging.AddSerilog(hostingContext, e =>
                    {
                        var context = e.Properties["SourceContext"].ToString();
                        if (e.Properties.ContainsKey("RequestPath") &&
                            !string.IsNullOrWhiteSpace(e.Properties["RequestPath"]?.ToString()) &&
                            (context.Contains(".Server.Kestrel") || context.Contains(".Core.IISHttpServer")))
                        {
                            return false;
                        }
                        return e.Level >= LogEventLevel.Error;
                    }));
                })
                .Build()
                .Run();
        }
    }
}
