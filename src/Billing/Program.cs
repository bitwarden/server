using Bit.Core.Settings;

namespace Bit.Billing;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        var startup = new Startup(builder.Environment, builder.Configuration);

        startup.ConfigureServices(builder.Services);

        var app = builder.Build();

        app.MapDefaultEndpoints();

        var settings = app.Services.GetRequiredService<GlobalSettings>();

        startup.Configure(app, app.Environment, app.Lifetime, settings);

        app.Run();
    //     Host
    //         .CreateDefaultBuilder(args)
    //         .ConfigureWebHostDefaults(webBuilder =>
    //         {
    //             webBuilder.UseStartup<Startup>();
    //             webBuilder.ConfigureLogging((hostingContext, logging) =>
    //                 logging.AddSerilog(hostingContext, (e, globalSettings) =>
    //                 {
    //                     var context = e.Properties["SourceContext"].ToString();
    //                     if (context.StartsWith("\"Bit.Billing.Jobs") || context.StartsWith("\"Bit.Core.Jobs"))
    //                     {
    //                         return e.Level >= globalSettings.MinLogLevel.BillingSettings.Jobs;
    //                     }

    //                     if (e.Properties.ContainsKey("RequestPath") &&
    //                         !string.IsNullOrWhiteSpace(e.Properties["RequestPath"]?.ToString()) &&
    //                         (context.Contains(".Server.Kestrel") || context.Contains(".Core.IISHttpServer")))
    //                     {
    //                         return false;
    //                     }

    //                     return e.Level >= globalSettings.MinLogLevel.BillingSettings.Default;
    //                 }));
    //         })
    //         .Build()
    //         .Run();
     }
}
