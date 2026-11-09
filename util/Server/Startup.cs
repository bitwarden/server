using System.Diagnostics.CodeAnalysis;
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
        IConfiguration configuration,
        ILogger<Startup> logger)
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
            var appIdLocation = configuration.GetValue<string>("appIdLocation");

            if (!string.IsNullOrEmpty(appIdLocation))
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/app-id.json", async context =>
                    {
                        var appId = await File.ReadAllTextAsync(appIdLocation);
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(appId);
                    });
                });
            }

            var provider = new WebVaultContentTypeProvider(
                new FileExtensionContentTypeProvider
                {
                    // TODO: This should be removed when asp.net natively support avif
                    Mappings = { [".avif"] = "image/avif" },
                });

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

    /// <summary>
    /// Adds content types for web vault files that have no extension to look up.
    /// </summary>
    private class WebVaultContentTypeProvider(IContentTypeProvider inner) : IContentTypeProvider
    {
        public bool TryGetContentType(string subpath, [MaybeNullWhen(false)] out string contentType)
        {
            // Apple requires this file to be served as JSON, but it has no file extension.
            if (string.Equals(subpath, "/.well-known/apple-app-site-association", StringComparison.Ordinal))
            {
                contentType = "application/json";
                return true;
            }

            return inner.TryGetContentType(subpath, out contentType);
        }
    }
}
