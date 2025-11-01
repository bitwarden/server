using System.Globalization;
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
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Repositories
        services.AddDatabaseRepositories(globalSettings);

        // Hosted Services
        services.AddAzureServiceBusListeners(globalSettings);
        services.AddHostedService<AzureQueueHostedService>();
    }

    public void Configure(IApplicationBuilder app)
    {
        IdentityModelEventSource.ShowPII = true;
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
