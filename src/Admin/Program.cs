using Bit.Core.Utilities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Serilog.Events;

namespace Bit.Admin
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
