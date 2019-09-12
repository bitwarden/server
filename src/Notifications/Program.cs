using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Bit.Core.Utilities;
using Serilog.Events;

namespace Bit.Notifications
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

                        if(e.Level == LogEventLevel.Error && 
                            e.MessageTemplate.Text == "Failed connection handshake.")
                        {
                            return false;
                        }

                        if(e.Level == LogEventLevel.Error &&
                            e.MessageTemplate.Text.StartsWith("Failed writing message."))
                        {
                            return false;
                        }

                        if(e.Level == LogEventLevel.Warning &&
                            e.MessageTemplate.Text.StartsWith("Heartbeat took longer"))
                        {
                            return false;
                        }

                        return e.Level >= LogEventLevel.Warning;
                    }))
                .Build()
                .Run();
        }
    }
}
