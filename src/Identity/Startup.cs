using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Bit.Core;
using Bit.Core.Utilities;
using Serilog.Events;

namespace Bit.Identity
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .AddSettingsConfiguration(env, "527b1fab-a4b5-465b-881a-f44f08c42899");
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

            // IdentityServer
            services.AddCustomIdentityServerServices(Environment, globalSettings);

            // Identity
            services.AddCustomIdentityServices(globalSettings);

            // Services
            services.AddBaseServices();
            services.AddDefaultServices();
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            loggerFactory
                .AddSerilog(env, appLifetime, globalSettings, (e) => e.Level >= LogEventLevel.Error)
                .AddConsole()
                .AddDebug();

            // Add IdentityServer to the request pipeline.
            app.UseIdentityServer();
        }
    }
}
