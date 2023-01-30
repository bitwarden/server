using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using AspNetCoreRateLimit;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Identity.Utilities;
using Bit.SharedWeb.Utilities;
using IdentityServer4.Extensions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Logging;
using Microsoft.OpenApi.Models;

namespace Bit.Identity;

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
        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);
        if (!globalSettings.SelfHosted)
        {
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimitOptions"));
            services.Configure<IpRateLimitPolicies>(Configuration.GetSection("IpRateLimitPolicies"));
        }

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Repositories
        services.AddDatabaseRepositories(globalSettings);

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Caching
        services.AddMemoryCache();
        services.AddDistributedCache(globalSettings);

        // Mvc
        services.AddMvc(config =>
        {
            config.Filters.Add(new ModelStateValidationFilterAttribute());
        });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Bitwarden Identity", Version = "v1" });
        });

        if (!globalSettings.SelfHosted)
        {
            services.AddIpRateLimiting(globalSettings);
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
            .AddDistributedIdentityServices(globalSettings)
            .AddAuthentication()
            .AddCookie(AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme)
            .AddOpenIdConnect("sso", "Single Sign On", options =>
            {
                options.Authority = globalSettings.BaseServiceUri.InternalSso;
                options.RequireHttpsMetadata = !Environment.IsDevelopment() &&
                    globalSettings.BaseServiceUri.InternalIdentity.StartsWith("https");
                options.ClientId = "oidc-identity";
                options.ClientSecret = globalSettings.OidcIdentityClientKey;
                options.ResponseMode = "form_post";

                options.SignInScheme = AuthenticationSchemes.BitwardenExternalCookieAuthenticationScheme;
                options.ResponseType = "code";
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = true;

                options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        // Pass domain_hint onto the sso idp
                        context.ProtocolMessage.DomainHint = context.Properties.Items["domain_hint"];
                        context.ProtocolMessage.Parameters.Add("organizationId", context.Properties.Items["organizationId"]);
                        if (context.Properties.Items.ContainsKey("user_identifier"))
                        {
                            context.ProtocolMessage.SessionState = context.Properties.Items["user_identifier"];
                        }

                        if (context.Properties.Parameters.Count > 0 &&
                            context.Properties.Parameters.TryGetValue(SsoTokenable.TokenIdentifier, out var tokenValue))
                        {
                            var token = tokenValue?.ToString() ?? "";
                            context.ProtocolMessage.Parameters.Add(SsoTokenable.TokenIdentifier, token);
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
        services.AddBaseServices(globalSettings);
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

        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

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
            app.UseSwagger();
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
