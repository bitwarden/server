using Bit.Core.Settings;

namespace Bit.Icons;

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
    }
}
