using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Bit.Core;
using Bit.Core.Utilities;
using AspNetCoreRateLimit;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Bit.Identity.Utilities;
using IdentityServer4.Extensions;

namespace Bit.Identity
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
            if (!globalSettings.SelfHosted)
            {
                services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimitOptions"));
                services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
            }

            // Data Protection
            services.AddCustomDataProtectionServices(Environment, globalSettings);

            // Repositories
            services.AddSqlServerRepositories(globalSettings);

            // Context
            services.AddScoped<CurrentContext>();

            // Caching
            services.AddMemoryCache();

            // Fido2
            services.AddFido2(options =>
            {
                options.ServerDomain = new Uri(globalSettings.BaseServiceUri.Vault).Host;
                options.ServerName = "Bitwarden";
                options.Origin = globalSettings.BaseServiceUri.Vault;
                options.TimestampDriftTolerance = 300000;
            });

            // Mvc
            services.AddMvc();

            if (!globalSettings.SelfHosted)
            {
                // Rate limiting
                services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
                services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
            }

            // Cookies
            if (Environment.IsDevelopment())
            {
                services.Configure<CookiePolicyOptions>(options =>
                {
                    options.MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
                    options.OnAppendCookie = ctx =>
                    {
                        ctx.CookieOptions.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
                    };
                });
            }

            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            // Authentication
            services
                .AddAuthentication()
                .AddOpenIdConnect("sso", "Single Sign On", options =>
                {
                    options.Authority = globalSettings.BaseServiceUri.InternalSso;
                    options.RequireHttpsMetadata = !Environment.IsDevelopment() &&
                        globalSettings.BaseServiceUri.InternalIdentity.StartsWith("https");
                    options.ClientId = "oidc-identity";
                    options.ClientSecret = globalSettings.OidcIdentityClientKey;
                    options.ResponseMode = "form_post";

                    options.SignInScheme = IdentityServer4.IdentityServerConstants.ExternalCookieAuthenticationScheme;
                    options.ResponseType = "code";

                    options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
                    {
                        OnRedirectToIdentityProvider = context =>
                        {
                            // Pass domain_hint onto the sso idp
                            context.ProtocolMessage.DomainHint = context.Properties.Items["domain_hint"];
                            if (context.Properties.Items.ContainsKey("user_identifier"))
                            {
                                context.ProtocolMessage.SessionState = context.Properties.Items["user_identifier"];
                            }
                            return Task.FromResult(0);
                        }
                    };
                });

            // IdentityServer
            services.AddCustomIdentityServerServices(Environment, globalSettings);

            // Identity
            services.AddCustomIdentityServices(globalSettings);

            // Services
            services.AddBaseServices();
            services.AddDefaultServices(globalSettings);
            services.AddCoreLocalizationServices();

            if (CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ConnectionString) &&
                CoreHelpers.SettingHasValue(globalSettings.ServiceBus.ApplicationCacheTopicName))
            {
                services.AddHostedService<Core.HostedServices.ApplicationCacheHostedService>();
            }

            // HttpClients
            services.AddHttpClient("InternalSso", client =>
            {
                client.BaseAddress = new Uri(globalSettings.BaseServiceUri.InternalSso);
            });
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            IHostApplicationLifetime appLifetime,
            GlobalSettings globalSettings,
            ILogger<Startup> logger)
        {
            IdentityModelEventSource.ShowPII = true;

            app.UseSerilog(env, appLifetime, globalSettings);

            if (!env.IsDevelopment())
            {
                var uri = new Uri(globalSettings.BaseServiceUri.Identity);
                app.Use(async (ctx, next) =>
                {
                    ctx.SetIdentityServerOrigin($"{uri.Scheme}://{uri.Host}");
                    await next();
                });
            }

            if (globalSettings.SelfHosted)
            {
                app.UsePathBase("/identity");
                app.UseForwardedHeaders(globalSettings);
            }

            // Default Middleware
            app.UseDefaultMiddleware(env, globalSettings);

            if (!globalSettings.SelfHosted)
            {
                // Rate limiting
                app.UseMiddleware<CustomIpRateLimitMiddleware>();
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseCookiePolicy();
            }

            // Add localization
            app.UseCoreLocalization();

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add routing
            app.UseRouting();

            // Add Cors
            app.UseCors(policy => policy.SetIsOriginAllowed(o => CoreHelpers.IsCorsOriginAllowed(o, globalSettings))
                .AllowAnyMethod().AllowAnyHeader().AllowCredentials());

            // Add current context
            app.UseMiddleware<CurrentContextMiddleware>();

            // Add IdentityServer to the request pipeline.
            app.UseIdentityServer();

            // Add Mvc stuff
            app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());

            // Log startup
            logger.LogInformation(Constants.BypassFiltersEventId, globalSettings.ProjectName + " started.");
        }
    }
}
