using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bit.Core;
using Stripe;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Routing;
using System.Globalization;

namespace Bit.Billing
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
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
            services.AddSqlServerRepositories(globalSettings);

            // PayPal Client
            services.AddSingleton<Utilities.PayPalIpnClient>();

            // BitPay Client
            services.AddSingleton<BitPayClient>();

            // Context
            services.AddScoped<CurrentContext>();

            // Identity
            services.AddCustomIdentityServices(globalSettings);
            //services.AddPasswordlessIdentityServices<ReadOnlyDatabaseIdentityUserStore>(globalSettings);

            // Services
            services.AddBaseServices();
            services.AddDefaultServices(globalSettings);

            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Mvc
            services.AddMvc(config =>
            {
                config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
            });
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

            // Jobs service, uncomment when we have some jobs to run
            // Jobs.JobsHostedService.AddJobsServices(services);
            // services.AddHostedService<Jobs.JobsHostedService>();
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            app.UseSerilog(env, appLifetime, globalSettings);

            if(env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAuthentication();
            app.UseStaticFiles();
            app.UseMvcWithDefaultRoute();
        }
    }
}
