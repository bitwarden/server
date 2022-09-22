using Bit.Core.Utilities;

namespace Bit.Admin;

public class Program
{
    public static void Main(string[] args)
    {
        Host
            .CreateDefaultBuilder(args)
            .ConfigureCustomAppConfiguration(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(o =>
                {
                    o.Limits.MaxRequestLineSize = 20_000;
                });
                webBuilder.UseStartup<Startup>();
                webBuilder.ConfigureLogging((hostingContext, logging) =>
                logging.AddSerilog(hostingContext, (e, globalSettings) =>
                {
                    var context = e.Properties["SourceContext"].ToString();
                    if (e.Properties.ContainsKey("RequestPath") &&
                        !string.IsNullOrWhiteSpace(e.Properties["RequestPath"]?.ToString()) &&
                        (context.Contains(".Server.Kestrel") || context.Contains(".Core.IISHttpServer")))
                    {
                        return false;
                    }
                    return e.Level >= globalSettings.MinLogLevel.AdminSettings.Default;
                }));
            })
            .Build()
            .Run();
    }
}
