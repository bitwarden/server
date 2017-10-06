using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Bit.Core;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Serilog.Events;

namespace Bit.Jobs
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddSettingsConfiguration(env, "bitwarden-Jobs")
                .AddEnvironmentVariables();
            Configuration = builder.Build();
            Environment = env;
        }

        public IConfigurationRoot Configuration { get; private set; }
        public IHostingEnvironment Environment { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions();

            // Settings
            var globalSettings = services.AddGlobalSettingsServices(Configuration);

            // Data Protection
            services.AddCustomDataProtectionServices(Environment, globalSettings);

            // Repositories
            services.AddSqlServerRepositories();

            // Context
            services.AddScoped<CurrentContext>();

            // Identity
            services.AddCustomIdentityServices(globalSettings);

            // Services
            services.AddBaseServices();
            services.AddDefaultServices(globalSettings);
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            loggerFactory
                .AddSerilog(env, appLifetime, globalSettings, e => e.Level >= LogEventLevel.Information)
                .AddConsole()
                .AddDebug();
        }
    }
}
