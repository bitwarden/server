using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Portal.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace Bit.Portal
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions();

            // Settings
            var globalSettings = services.AddGlobalSettingsServices(Configuration);
            services.Configure<PortalSettings>(Configuration.GetSection("PortalSettings"));

            // Data Protection
            services.AddCustomDataProtectionServices(Environment, globalSettings);

            // Repositories
            services.AddSqlServerRepositories(globalSettings);

            // Context
            services.AddScoped<EnterprisePortalCurrentContext>();
            services.AddScoped<ICurrentContext, CurrentContext>((serviceProvider) =>
                serviceProvider.GetService<EnterprisePortalCurrentContext>());

            // Fido2
            services.AddFido2(options =>
            {
                options.ServerDomain = new Uri(globalSettings.BaseServiceUri.Vault).Host;
                options.ServerName = "Bitwarden";
                options.Origin = globalSettings.BaseServiceUri.Vault;
                options.TimestampDriftTolerance = 300000;
            });

            // Identity
            services.AddEnterprisePortalTokenIdentityServices();
            if (globalSettings.SelfHosted)
            {
                services.ConfigureApplicationCookie(options =>
                {
                    options.Cookie.Path = "/portal";
                });
            }

            // Services
            services.AddBaseServices();
            services.AddDefaultServices(globalSettings);
            services.AddCoreLocalizationServices();

            // Fido2
            services.AddFido2(options =>
            {
                options.ServerDomain = new Uri(globalSettings.BaseServiceUri.Vault).Host;
                options.ServerName = "Bitwarden";
                options.Origin = globalSettings.BaseServiceUri.Vault;
                options.TimestampDriftTolerance = 300000;
            });

            // Mvc
            services.AddControllersWithViews()
                .AddViewAndDataAnnotationLocalization();
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            IHostApplicationLifetime appLifetime,
            GlobalSettings globalSettings,
            ILogger<Startup> logger)
        {
            app.UseSerilog(env, appLifetime, globalSettings);

            if (globalSettings.SelfHosted)
            {
                app.UsePathBase("/portal");
                app.UseForwardedHeaders(globalSettings);
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCoreLocalization();

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add routing
            app.UseRouting();

            // Add authentication and authorization to the request pipeline.
            app.UseAuthentication();
            app.UseAuthorization();

            // Add current context
            app.UseMiddleware<EnterprisePortalCurrentContextMiddleware>();

            // Add endpoints to the request pipeline.
            app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());

            // Log startup
            logger.LogInformation(Constants.BypassFiltersEventId, globalSettings.ProjectName + " started.");
        }
    }
}
