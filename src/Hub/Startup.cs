using System.Security.Claims;
using Bit.Core;
using Bit.Core.IdentityServer;
using Bit.Core.Services;
using Bit.Core.Utilities;
using IdentityModel;
using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using Serilog.Events;

namespace Bit.Hub
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
            services
                .AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
                .AddIdentityServerAuthentication(options =>
                {
                    options.Authority = globalSettings.BaseServiceUri.InternalIdentity;
                    options.RequireHttpsMetadata = !Environment.IsDevelopment() &&
                        globalSettings.BaseServiceUri.InternalIdentity.StartsWith("https");
                    options.TokenRetriever = TokenRetrieval.FromAuthorizationHeaderOrQueryString();
                    options.NameClaimType = ClaimTypes.Email;
                    options.SupportedTokens = SupportedTokens.Jwt;
                });

            services.AddAuthorization(config =>
            {
                config.AddPolicy("Application", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(JwtClaimTypes.AuthenticationMethod, "Application");
                });
            });

            // SignalR
            services.AddSignalR();
            services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();

            // Mvc
            services.AddMvc();

            // Hosted Services
            services.AddHostedService<TimedHostedService>();
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
            app.UseSignalR(routes =>
            {
                routes.MapHub<SyncHub>("/sync");
            });

            // Add MVC to the request pipeline.
            app.UseMvc();
        }
    }
}
