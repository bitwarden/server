using Bit.Core.Utilities;
using Serilog.Events;

namespace Bit.Notifications;

public class Program
{
    public static void Main(string[] args)
    {
        Host
            .CreateDefaultBuilder(args)
            .ConfigureCustomAppConfiguration(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.ConfigureLogging((hostingContext, logging) =>
                { 
                    logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
                    logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
                    logging.AddSerilog(hostingContext, (e, globalSettings) =>
                    {
                        return e.Level >= globalSettings.MinLogLevel.NotificationsSettings.Default;
                    });
                });
            })
            .Build()
            .Run();
    }
}
