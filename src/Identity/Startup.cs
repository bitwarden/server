using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Bit.Core;
using Bit.Core.Utilities;
using AspNetCoreRateLimit;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Bit.Identity
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; private set; }
        public IWebHostEnvironment Environment { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions();

            // Settings
            var globalSettings = services.AddGlobalSettingsServices(Configuration);
            if (!globalSettings.SelfHosted)
            {
                services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimitOptions"));
                services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
            }

            // Data Protection
            services.AddCustomDataProtectionServices(Environment, globalSettings);

            // Repositories
            services.AddSqlServerRepositories(globalSettings);

            // Context
            services.AddScoped<CurrentContext>();

            // Caching
            services.AddMemoryCache();

            if (!globalSettings.SelfHosted)
            {
                // Rate limiting
                services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
                services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
            }

            // IdentityServer
            services.AddCustomIdentityServerServices(Environment, globalSettings);

            // Identity
            services.AddCustomIdentityServices(globalSettings);

            // Services
            services.AddBaseServices();
            services.AddDefaultServices(globalSettings);

            if (CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ConnectionString) &&
                CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ApplicationCacheTopicName))
            {
                services.AddHostedService<Core.HostedServices.ApplicationCacheHostedService>();
            }
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            IHostApplicationLifetime appLifetime,
            GlobalSettings globalSettings,
            ILogger<Startup> logger)
        {
            app.UseSerilog(env, appLifetime, globalSettings);

            // Default Middleware
            app.UseDefaultMiddleware(env, globalSettings);

            if (!globalSettings.SelfHosted)
            {
                // Rate limiting
                app.UseMiddleware<CustomIpRateLimitMiddleware>();
            }
            else
            {
                app.UseForwardedHeaders(globalSettings);
            }

            // Add current context
            app.UseMiddleware<CurrentContextMiddleware>();

            // Add IdentityServer to the request pipeline.
            app.UseIdentityServer();

            // Log startup
            logger.LogInformation(Constants.BypassFiltersEventId, globalSettings.ProjectName + " started.");
        }
    }
}
