using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Bit.Core.Utilities;
using Serilog.Events;

namespace Bit.Events
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
