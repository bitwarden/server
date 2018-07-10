using System;
using Bit.Core;
using Bit.Core.Utilities;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Serilog.Events;

namespace Bit.Icons
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions();

            // Settings
            var globalSettings = services.AddGlobalSettingsServices(Configuration);
            var iconsSettings = new IconsSettings();
            ConfigurationBinder.Bind(Configuration.GetSection("IconsSettings"), iconsSettings);
            services.AddSingleton(s => iconsSettings);

            // Cache
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = iconsSettings.CacheSizeLimit;
            });

            // Services
            services.AddSingleton<IDomainMappingService, DomainMappingService>();
            services.AddSingleton<IIconFetchingService, IconFetchingService>();

            // Mvc
            services.AddMvc();
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            loggerFactory.AddSerilog(app, env, appLifetime, globalSettings, (e) => e.Level >= LogEventLevel.Error);

            if(env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Use(async (context, next) =>
            {
                context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromDays(7)
                };
                await next();
            });

            app.UseMvc();
        }
    }
}
