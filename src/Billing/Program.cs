using Microsoft.AspNetCore.Hosting;
using Bit.Core.Utilities;
using Serilog.Events;
using Microsoft.Extensions.Hosting;

namespace Bit.Billing
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
                            if(e.Level == LogEventLevel.Information &&
                                (context.StartsWith("\"Bit.Billing.Jobs") || context.StartsWith("\"Bit.Core.Jobs")))
                            {
                                return true;
                            }

                            if(e.Properties.ContainsKey("RequestPath") &&
                                !string.IsNullOrWhiteSpace(e.Properties["RequestPath"]?.ToString()) &&
                                (context.Contains(".Server.Kestrel") || context.Contains(".Core.IISHttpServer")))
                            {
                                return false;
                            }

                            return e.Level >= LogEventLevel.Warning;
                        }));
                })
                .Build()
                .Run();
        }
    }
}
