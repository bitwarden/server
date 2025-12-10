using Bit.Core.Utilities;
#if DEBUG
using Bit.ServiceDefaults;
#endif

namespace Bit.Identity;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = CreateHostBuilder(args);

#if DEBUG
        builder.AddServiceDefaults();
#endif

        builder.Build().Run();

    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host
            .CreateDefaultBuilder(args)
            .ConfigureCustomAppConfiguration(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .AddSerilogFileLogging();
    }
}
