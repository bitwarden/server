using Microsoft.AspNetCore.Hosting;
using Bit.Core.Utilities;
using Serilog.Events;
using AspNetCoreRateLimit;
using Microsoft.Extensions.Hosting;

namespace Bit.Identity
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Host
                .CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureLogging((hostingContext, logging) =>
                        logging.AddSerilog(hostingContext, e =>
                        {
                            var context = e.Properties["SourceContext"].ToString();
                            if (context.Contains(typeof(IpRateLimitMiddleware).FullName) &&
                                e.Level == LogEventLevel.Information)
                            {
                                return true;
                            }

                            if (context.Contains("IdentityServer4.Validation.TokenValidator") ||
                                context.Contains("IdentityServer4.Validation.TokenRequestValidator"))
                            {
                                return e.Level > LogEventLevel.Error;
                            }

                            return e.Level >= LogEventLevel.Error;
                        }));
                })
                .Build()
                .Run();
        }
    }
}
