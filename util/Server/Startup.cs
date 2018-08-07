using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Server
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

        public void ConfigureServices(IServiceCollection services)
        { }

        public void Configure(
            IApplicationBuilder app,
            ILoggerFactory loggerFactory,
            IConfiguration configuration)
        {
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
                var options = new DefaultFilesOptions();
                options.DefaultFileNames.Clear();
                options.DefaultFileNames.Add("index.html");
                app.UseDefaultFiles(options);
                app.UseStaticFiles(new StaticFileOptions
                {
                    OnPrepareResponse = ctx =>
                    {
                        System.Console.WriteLine("Path: " + ctx.Context.Request.Path.Value);
                        if(!ctx.Context.Request.Path.HasValue)
                        {
                            return;
                        }
                        var path = ctx.Context.Request.Path.Value;
                        if(_longCachedPaths.Any(ext => path.StartsWith(ext)))
                        {
                            // 14 days
                            ctx.Context.Response.Headers.Append("Cache-Control", "max-age=1209600");
                        }
                        if(_mediumCachedPaths.Any(ext => path.StartsWith(ext)))
                        {
                            // 7 days
                            ctx.Context.Response.Headers.Append("Cache-Control", "max-age=604800");
                        }
                    }
                });
            }
        }
    }
}
