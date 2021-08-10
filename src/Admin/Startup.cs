using System;
using System.Globalization;
using Bit.Core.Context;
using Bit.Core.Identity;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stripe;

#if !OSS
using Bit.CommCore.Utilities;
#endif

namespace Bit.Admin
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
            services.Configure<AdminSettings>(Configuration.GetSection("AdminSettings"));

            // Data Protection
            services.AddCustomDataProtectionServices(Environment, globalSettings);

            // Stripe Billing
            StripeConfiguration.ApiKey = globalSettings.StripeApiKey;

            // Repositories
            services.AddSqlServerRepositories(globalSettings);

            // Context
            services.AddScoped<ICurrentContext, CurrentContext>();

            // Identity
            services.AddPasswordlessIdentityServices<ReadOnlyEnvIdentityUserStore>(globalSettings);
            services.Configure<SecurityStampValidatorOptions>(options =>
            {
                options.ValidationInterval = TimeSpan.FromMinutes(5);
            });
            if (globalSettings.SelfHosted)
            {
                services.ConfigureApplicationCookie(options =>
                {
                    options.Cookie.Path = "/admin";
                });
            }

            // Services
            services.AddBaseServices();
            services.AddDefaultServices(globalSettings);
            
            #if OSS
                services.AddOosServices();
            #else
                services.AddCommCoreServices();
            #endif

            // Mvc
            services.AddMvc(config =>
            {
                config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
            });
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

            // Jobs service
            Jobs.JobsHostedService.AddJobsServices(services, globalSettings.SelfHosted);
            services.AddHostedService<Jobs.JobsHostedService>();
            if (globalSettings.SelfHosted)
            {
                services.AddHostedService<HostedServices.DatabaseMigrationHostedService>();
            }
            else
            {
                if (CoreHelpers.SettingHasValue(globalSettings.Storage.ConnectionString))
                {
                    services.AddHostedService<HostedServices.AzureQueueBlockIpHostedService>();
                }
                else if (CoreHelpers.SettingHasValue(globalSettings.Amazon?.AccessKeySecret))
                {
                    services.AddHostedService<HostedServices.AmazonSqsBlockIpHostedService>();
                }
                if (CoreHelpers.SettingHasValue(globalSettings.Mail.ConnectionString))
                {
                    services.AddHostedService<HostedServices.AzureQueueMailHostedService>();
                }
            }
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            IHostApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            app.UseSerilog(env, appLifetime, globalSettings);

            if (globalSettings.SelfHosted)
            {
                app.UsePathBase("/admin");
                app.UseForwardedHeaders(globalSettings);
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
        }
    }
}
