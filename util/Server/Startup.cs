using System.Globalization;
using Microsoft.AspNetCore.StaticFiles;

namespace Bit.Server;

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
        if (configuration.GetValue<bool?>("serveUnknown") ?? false)
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
        else if (configuration.GetValue<bool?>("webVault") ?? false)
        {
            // TODO: This should be removed when asp.net natively support avif
            var provider = new FileExtensionContentTypeProvider { Mappings = { [".avif"] = "image/avif" } };

            var options = new DefaultFilesOptions();
            options.DefaultFileNames.Clear();
            options.DefaultFileNames.Add("index.html");
            app.UseDefaultFiles(options);
            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = provider,
                OnPrepareResponse = ctx =>
                {
                    if (!ctx.Context.Request.Path.HasValue ||
                        ctx.Context.Response.Headers.ContainsKey("Cache-Control"))
                    {
                        return;
                    }
                    var path = ctx.Context.Request.Path.Value;
                    if (_longCachedPaths.Any(ext => path.StartsWith(ext)))
                    {
                        // 14 days
                        ctx.Context.Response.Headers.Append("Cache-Control", "max-age=1209600");
                    }
                    if (_mediumCachedPaths.Any(ext => path.StartsWith(ext)))
                    {
                        // 7 days
                        ctx.Context.Response.Headers.Append("Cache-Control", "max-age=604800");
                    }
                }
            });
        }
        else
        {
            app.UseFileServer();
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/alive",
                    async context => await context.Response.WriteAsync(System.DateTime.UtcNow.ToString()));
            });
        }
    }
}
