using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Bit.Core;
using Stripe;

namespace Bit.Billing
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"settings.{env.EnvironmentName}.json", optional: true);

            if(env.IsDevelopment())
            {
                builder.AddUserSecrets<Startup>();
            }

            builder.AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions();

            // Settings
            var globalSettings = new GlobalSettings();
            ConfigurationBinder.Bind(Configuration.GetSection("GlobalSettings"), globalSettings);
            services.AddSingleton(s => globalSettings);
            services.Configure<BillingSettings>(Configuration.GetSection("BillingSettings"));

            // Stripe Billing
            StripeConfiguration.SetApiKey(globalSettings.StripeApiKey);

            // Repositories
            services.AddSqlServerRepositories();

            // Context
            services.AddScoped<CurrentContext>();

            // Services
            services.AddBaseServices();
            services.AddDefaultServices();

            // Mvc
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseMvc();
        }
    }
}
