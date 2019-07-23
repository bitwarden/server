using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Bit.Core.Utilities;
using Serilog.Events;

namespace Bit.Icons
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebHost
                .CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .ConfigureLogging((hostingContext, logging) =>
                    logging.AddSerilog(hostingContext, e => e.Level >= LogEventLevel.Error))
                .Build()
                .Run();
        }
    }
}
