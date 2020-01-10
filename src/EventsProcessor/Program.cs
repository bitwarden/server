using Microsoft.AspNetCore.Hosting;
using Bit.Core.Utilities;
using Serilog.Events;
using Microsoft.Extensions.Hosting;

namespace Bit.EventsProcessor
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
                        logging.AddSerilog(hostingContext, e => e.Level >= LogEventLevel.Warning));
                })
                .Build()
                .Run();
        }
    }
}
