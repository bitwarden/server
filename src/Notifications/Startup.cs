using System.Collections.Generic;
using System.Globalization;
using Bit.Core;
using Bit.Core.Utilities;
using IdentityModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;

namespace Bit.Notifications
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IConfiguration configuration)
        {
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
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
            var signalRServerBuilder = services.AddSignalR().AddMessagePackProtocol(options =>
            {
                options.FormatterResolvers = new List<MessagePack.IFormatterResolver>()
                {
                    MessagePack.Resolvers.ContractlessStandardResolver.Instance
                };
            });
            if(!string.IsNullOrWhiteSpace(globalSettings.Notifications?.AzureSignalRConnectionString))
            {
                signalRServerBuilder.AddAzureSignalR(globalSettings.Notifications.AzureSignalRConnectionString);
            }
            services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
            services.AddSingleton<ConnectionCounter>();

            // Mvc
            services.AddMvc();

            services.AddHostedService<HeartbeatHostedService>();
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
            IApplicationLifetime appLifetime,
            GlobalSettings globalSettings)
        {
            IdentityModelEventSource.ShowPII = true;
            app.UseSerilog(env, appLifetime, globalSettings);

            if(env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Add Cors
            app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

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
                    options.ApplicationMaxBufferSize = 2048; // client => server messages are not even used
                    options.TransportMaxBufferSize = 4096;
                }));
            }

            // Add MVC to the request pipeline.
            app.UseMvc();
        }
    }
}
