using Bit.Core;
using Bit.Core.Utilities;
using IdentityModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using Serilog.Events;

namespace Bit.Notifications
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IConfiguration configuration)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions();

            // Settings
            var globalSettings = services.AddGlobalSettingsServices(Configuration);

            // Repositories
            services.AddSqlServerRepositories(globalSettings);

            // Context
            services.AddScoped<CurrentContext>();

            // Identity
            services.AddIdentityAuthenticationServices(globalSettings, Environment, config =>
            {
                config.AddPolicy("Application", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(JwtClaimTypes.AuthenticationMethod, "Application");
                    policy.RequireClaim(JwtClaimTypes.Scope, "api");
                });
                config.AddPolicy("Internal", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(JwtClaimTypes.Scope, "internal");
                });
            });

            // SignalR
            if(!string.IsNullOrWhiteSpace(globalSettings.Notifications?.AzureSignalRConnectionString))
            {
                services.AddSignalR().AddAzureSignalR(globalSettings.Notifications.AzureSignalRConnectionString);
            }
            else
            {
                services.AddSignalR();
            }
            services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();

            // Mvc
            services.AddMvc();

            // Hosted Services
            if(!globalSettings.SelfHosted &&
                CoreHelpers.SettingHasValue(globalSettings.Notifications?.ConnectionString))
            {
                services.AddHostedService<AzureQueueHostedService>();
            }
        }

        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            IdentityModelEventSource.ShowPII = true;
            loggerFactory.AddSerilog(app, env, appLifetime, globalSettings, (e) =>
            {
                var context = e.Properties["SourceContext"].ToString();
                if(context.Contains("IdentityServer4.Validation.TokenValidator") ||
                    context.Contains("IdentityServer4.Validation.TokenRequestValidator"))
                {
                    return e.Level > LogEventLevel.Error;
                }

                return e.Level >= LogEventLevel.Error;
            });

            if(env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Default Middleware
            app.UseDefaultMiddleware(env);

            // Add Cors
            app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().AllowCredentials());

            // Add authentication to the request pipeline.
            app.UseAuthentication();

            // Add SignlarR
            if(!string.IsNullOrWhiteSpace(globalSettings.Notifications?.AzureSignalRConnectionString))
            {
                app.UseAzureSignalR(routes => routes.MapHub<NotificationsHub>("/hub"));
            }
            else
            {
                app.UseSignalR(routes => routes.MapHub<NotificationsHub>("/hub"));
            }

            // Add MVC to the request pipeline.
            app.UseMvc();
        }
    }
}
