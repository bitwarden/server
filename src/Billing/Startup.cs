using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Bit.Core;
using Stripe;
using Bit.Core.Utilities;
using Serilog.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bit.Billing.Utilities;

namespace Bit.Billing
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
            services.Configure<BillingSettings>(Configuration.GetSection("BillingSettings"));

            // Stripe Billing
            StripeConfiguration.SetApiKey(globalSettings.StripeApiKey);

            // Repositories
            services.AddSqlServerRepositories();

            // Context
            services.AddScoped<CurrentContext>();

            // Identity
            services.AddCustomIdentityServices(globalSettings);

            // Services
            services.AddBaseServices();
            services.AddDefaultServices(globalSettings);

            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Mvc
            services.AddMvc(config =>
            {
                config.Filters.Add(new ExceptionHandlerFilterAttribute());
            });
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings,
            ILoggerFactory loggerFactory)
        {
            loggerFactory.AddSerilog(env, appLifetime, globalSettings, (e) => e.Level >= LogEventLevel.Error);

            if(env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Default Middleware
            app.UseDefaultMiddleware(env);

            app.UseMvc();
        }
    }
}
