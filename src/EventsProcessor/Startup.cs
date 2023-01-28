using System.Globalization;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.IdentityModel.Logging;

namespace Bit.EventsProcessor;

public class Startup
{
    public Startup(IWebHostEnvironment env, IConfiguration configuration)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        Configuration = configuration;
        Environment = env;
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment Environment { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Options
        services.AddOptions();

        // Settings
        services.AddGlobalSettingsServices(Configuration, Environment);

        // Hosted Services
        services.AddHostedService<AzureQueueHostedService>();
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings)
    {
        IdentityModelEventSource.ShowPII = true;
        app.UseSerilog(env, appLifetime, globalSettings);
        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/alive",
                async context => await context.Response.WriteAsJsonAsync(System.DateTime.UtcNow));
            endpoints.MapGet("/now",
                async context => await context.Response.WriteAsJsonAsync(System.DateTime.UtcNow));
            endpoints.MapGet("/version",
                async context => await context.Response.WriteAsJsonAsync(AssemblyHelpers.GetVersion()));

        });
    }
}
