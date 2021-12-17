using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Bit.Icons
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Host
                    .CreateDefaultBuilder(args)
                    .UseSerilog((context, configuration) =>
                    {
                        configuration.ReadFrom.Configuration(context.Configuration);
                    })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseStartup<Startup>();
                    })
                    .Build()
                    .Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
