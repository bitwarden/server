// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = CreateWebHostBuilder(args);
        var host = builder.Build();
        host.Run();
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();

        var builder = new WebHostBuilder()
            .UseConfiguration(config)
            .UseKestrel()
            .UseStartup<Startup>()
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConsole().AddDebug();
            })
            .ConfigureKestrel((context, options) => { });

        var contentRoot = config.GetValue<string>("contentRoot");
        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            builder.UseContentRoot(contentRoot);
        }
        else
        {
            builder.UseContentRoot(Directory.GetCurrentDirectory());
        }

        var webRoot = config.GetValue<string>("webRoot");
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            builder.UseWebRoot(webRoot);
        }

        return builder;
    }
}
