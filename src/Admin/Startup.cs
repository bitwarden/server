using System;
using Bit.Core;
using Bit.Core.Identity;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using Stripe;

namespace Bit.Admin
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IConfiguration configuration)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; private set; }
        public IHostingEnvironment Environment { get; set; }

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
            StripeConfiguration.SetApiKey(globalSettings.StripeApiKey);

            // Repositories
            services.AddSqlServerRepositories(globalSettings);

            // Context
            services.AddScoped<CurrentContext>();

            // Identity
            services.AddPasswordlessIdentityServices<ReadOnlyEnvIdentityUserStore>(globalSettings);
            services.Configure<SecurityStampValidatorOptions>(options =>
            {
                options.ValidationInterval = TimeSpan.FromMinutes(5);
            });
            if(globalSettings.SelfHosted)
            {
                services.ConfigureApplicationCookie(options =>
                {
                    options.Cookie.Path = "/admin";
                });
            }

            // Services
            services.AddBaseServices();
            services.AddDefaultServices(globalSettings);

            // Mvc
            services.AddMvc(config =>
            {
                config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
            });
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

            // Jobs service
            Jobs.JobsHostedService.AddJobsServices(services);
            services.AddHostedService<Jobs.JobsHostedService>();
            if(!globalSettings.SelfHosted)
            {
                if(CoreHelpers.SettingHasValue(globalSettings.Storage.ConnectionString))
                {
                    services.AddHostedService<HostedServices.AzureQueueBlockIpHostedService>();
                }
                else if(CoreHelpers.SettingHasValue(globalSettings.Amazon?.AccessKeySecret))
                {
                    services.AddHostedService<HostedServices.AmazonSqsBlockIpHostedService>();
                }
            }
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings,
            ILoggerFactory loggerFactory)
        {
            loggerFactory.AddSerilog(app, env, appLifetime, globalSettings, (e) => e.Level >= LogEventLevel.Error);

            if(globalSettings.SelfHosted)
            {
                app.UsePathBase("/admin");
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                });
            }

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
