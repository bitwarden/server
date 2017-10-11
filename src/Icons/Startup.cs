using System;
using Bit.Icons.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            var iconsSettings = new IconsSettings();
            ConfigurationBinder.Bind(Configuration.GetSection("IconsSettings"), iconsSettings);
            services.AddSingleton(s => iconsSettings);

            // Cache
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = iconsSettings.CacheSizeLimit;
            });
            services.AddResponseCaching();

            // Services
            services.AddSingleton<IDomainMappingService, DomainMappingService>();

            // Mvc
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if(env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseResponseCaching();
            app.UseMvc();
        }
    }
}
