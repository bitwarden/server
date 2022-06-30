using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Attachments
{
    public class Startup
    {
        private readonly List<string> _longCachedPaths = new List<string>
        {
            "/app/", "/locales/", "/fonts/", "/connectors/", "/scripts/"
        };
        private readonly List<string> _mediumCachedPaths = new List<string>
        {
            "/images/"
        };

        public Startup()
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();
        }

        public void Configure(
            IApplicationBuilder app,
            IConfiguration configuration)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream"
            });
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/alive",
                    async context => await context.Response.WriteAsync(System.DateTime.UtcNow.ToString()));
            });
        }
    }
}
