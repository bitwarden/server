using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Server
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        { }

        public void Configure(
            IApplicationBuilder app,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            TelemetryConfiguration telemetryConfiguration)
        {
            telemetryConfiguration.DisableTelemetry = true;
            loggerFactory
                .AddConsole()
                .AddDebug();

            var serveUnknown = configuration.GetValue<bool?>("serveUnknown") ?? false;
            if(serveUnknown)
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    ServeUnknownFileTypes = true,
                    DefaultContentType = "application/octet-stream"
                });
            }
            else
            {
                app.UseFileServer();
            }
        }
    }
}
