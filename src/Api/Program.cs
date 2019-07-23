using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Bit.Core.Utilities;
using Serilog.Events;
using Microsoft.IdentityModel.Tokens;
using AspNetCoreRateLimit;

namespace Bit.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebHost
                .CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .ConfigureLogging((hostingContext, logging) =>
                    logging.AddSerilog(hostingContext, e =>
                    {
                        var context = e.Properties["SourceContext"].ToString();
                        if(e.Exception != null && (e.Exception.GetType() == typeof(SecurityTokenValidationException) ||
                            e.Exception.Message == "Bad security stamp."))
                        {
                            return false;
                        }

                        if(e.Level == LogEventLevel.Information && context.Contains(typeof(IpRateLimitMiddleware).FullName))
                        {
                            return true;
                        }

                        if(context.Contains("IdentityServer4.Validation.TokenValidator") ||
                            context.Contains("IdentityServer4.Validation.TokenRequestValidator"))
                        {
                            return e.Level > LogEventLevel.Error;
                        }

                        return e.Level >= LogEventLevel.Error;
                    }))
                .Build()
                .Run();
        }
    }
}
