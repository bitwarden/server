using System.Collections.Generic;
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
                services.AddSignalR().AddMessagePackProtocol(options =>
                {
                    options.FormatterResolvers = new List<MessagePack.IFormatterResolver>()
                    {
                        MessagePack.Resolvers.ContractlessStandardResolver.Instance
                    };
                });
            }
            services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
            services.AddSingleton<ConnectionCounter>();

            // Mvc
            services.AddMvc();

            if(!globalSettings.SelfHosted)
            {
                // Hosted Services
                Jobs.JobsHostedService.AddJobsServices(services);
                services.AddHostedService<Jobs.JobsHostedService>();
                if(CoreHelpers.SettingHasValue(globalSettings.Notifications?.ConnectionString))
                {
                    services.AddHostedService<AzureQueueHostedService>();
                }
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

                if(e.Level == LogEventLevel.Error && e.MessageTemplate.Text == "Failed connection handshake.")
                {
                    return false;
                }

                return e.Level >= LogEventLevel.Error;
            });

            if(env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

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
                app.UseSignalR(routes => routes.MapHub<NotificationsHub>("/hub", options =>
                {
                    options.ApplicationMaxBufferSize = 20; // client => server messages are not even used
                    options.TransportMaxBufferSize = 2048;
                }));
            }

            // Add MVC to the request pipeline.
            app.UseMvc();
        }
    }
}
