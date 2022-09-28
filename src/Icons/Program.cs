using Bit.Core.Utilities;

namespace Bit.Icons;

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
                    logging.AddSerilog(hostingContext, (e, globalSettings) => e.Level >= globalSettings.MinLogLevel.IconsSettings.Default));
            })
            .Build()
            .Run();
    }
}
