using System;
using System.Globalization;
using Bit.Core.Context;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Stripe;

namespace Bit.Scim
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions();

            // Settings
            var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);
            services.Configure<ScimSettings>(Configuration.GetSection("ScimSettings"));

            // Stripe Billing
            StripeConfiguration.ApiKey = globalSettings.Stripe.ApiKey;
            StripeConfiguration.MaxNetworkRetries = globalSettings.Stripe.MaxNetworkRetries;

            // Repositories
            services.AddSqlServerRepositories(globalSettings);

            // Context
            services.AddScoped<ICurrentContext, CurrentContext>();

            // Identity
            services.AddCustomIdentityServices(globalSettings);

            // Services
            services.AddBaseServices(globalSettings);
            services.AddDefaultServices(globalSettings);

            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Mvc
            services.AddMvc(config =>
            {
                config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
            });
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

            // Authentication
            services.AddAuthentication();
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            IHostApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            app.UseSerilog(env, appLifetime, globalSettings);

            // Add general security headers
            app.UseMiddleware<SecurityHeadersMiddleware>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
        }
    }
}
