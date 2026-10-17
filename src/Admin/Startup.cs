using System.Globalization;
using Bit.Admin.Auth.IdentityServer;
using Bit.Admin.IdentityServer;
using Bit.Core.Context;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Stripe;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bit.Admin.Auth.Services;
using Bit.Admin.Controllers;
using Bit.Admin.Services;
using Bit.Core.Billing.Extensions;

#if !OSS
using Bit.Commercial.Core.Utilities;
using Bit.Commercial.Infrastructure.EntityFramework.SecretsManager;
#endif

namespace Bit.Admin;

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
        var adminSettingsSection = Configuration.GetSection("AdminSettings");
        var adminSettings = adminSettingsSection.Get<AdminSettings>() ?? new AdminSettings();
        services.Configure<AdminSettings>(adminSettingsSection);

        // Data Protection
        services.AddCustomDataProtectionServices(Environment, globalSettings);

        // Stripe Billing
        StripeConfiguration.ApiKey = globalSettings.Stripe.ApiKey;
        StripeConfiguration.MaxNetworkRetries = globalSettings.Stripe.MaxNetworkRetries;

        // Repositories
        var databaseProvider = services.AddDatabaseRepositories(globalSettings);
        switch (databaseProvider)
        {
            case Core.Enums.SupportedDatabaseProviders.SqlServer:
                services.AddSingleton<IDbMigrator, Migrator.SqlServerDbMigrator>();
                break;
            case Core.Enums.SupportedDatabaseProviders.MySql:
                services.AddSingleton<IDbMigrator, MySqlMigrations.MySqlDbMigrator>();
                break;
            case Core.Enums.SupportedDatabaseProviders.Postgres:
                services.AddSingleton<IDbMigrator, PostgresMigrations.PostgresDbMigrator>();
                break;
            case Core.Enums.SupportedDatabaseProviders.Sqlite:
                services.AddSingleton<IDbMigrator, SqliteMigrations.SqliteDbMigrator>();
                break;
            default:
                break;
        }
        services.AddTestPlayIdTracking(globalSettings);

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Identity
        services.AddPasswordlessIdentityServices<ReadOnlyEnvIdentityUserStore>(globalSettings, adminSettings);
        services.AddAdminUpstreamOidc(adminSettings);
        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            // This interval defines the maximum delay in access changes
            // being reflected in the application. Applies to the Identity application cookie
            // (used by the passwordless-email path), which has a backing user store with a
            // SecurityStamp to validate. The SSO cookie added below has no backing store,
            // so this validator is not wired to it.
            options.ValidationInterval = TimeSpan.FromSeconds(30);
        });
        services.AddAuthentication()
            .AddCookie(AdminAuthenticationSchemes.SsoCookie, options =>
            {
                // Shared with the Identity cookie so a session timeout on either lands on the
                // same login page.
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login?accessDenied=true";
                options.Cookie.Name = $"Bitwarden_{globalSettings.ProjectName}.SSO";
                options.Cookie.HttpOnly = true;
                // Secure=Always is safe here because this cookie is only ever issued at the end
                // of an SSO sign-in, and SSO already requires HTTPS end-to-end (the OIDC
                // handler sets RequireHttpsMetadata=true and its correlation/nonce cookies are
                // SameSite=None, which browsers only honor over HTTPS).
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                // Lax matches the Identity cookie: required so the cookie is sent on the
                // top-level GET redirect back from the OIDC callback.
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(2);
                options.SlidingExpiration = true;
                options.ReturnUrlParameter = "returnUrl";
                // Deliberately NO SecurityStampValidator wired to this scheme. The IdP owns
                // identity, there is no local user record, and no stamp to compare against.
                // Session lifetime is bounded by the cookie expiry above and by RP-initiated
                // logout on sign-out.
            });
        services.AddAuthorization(options =>
        {
            // Try the Identity cookie first, then the SSO cookie. The PolicyEvaluator calls
            // AuthenticateAsync on each listed scheme and sets HttpContext.User to the first
            // successful result, so both passwordless and SSO sessions satisfy bare [Authorize]
            // and are visible to AccessControlService. Each scheme is actually decrypted and
            // validated - no cookie-name presence heuristic that would short-circuit a valid
            // session when a stale cookie from the other scheme happens to be on the request.
            //
            // DefaultAuthenticateScheme / DefaultChallengeScheme stay at Identity's application
            // cookie (set by AddIdentity), so bare [Authorize] redirects to /login via Identity
            // and SignInManager still writes to the Identity cookie.
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    IdentityConstants.ApplicationScheme,
                    AdminAuthenticationSchemes.SsoCookie)
                .RequireAuthenticatedUser()
                .Build();
            options.FallbackPolicy = options.DefaultPolicy;
        });
        if (globalSettings.SelfHosted)
        {
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Path = "/admin";
            });
            services.Configure<CookieAuthenticationOptions>(AdminAuthenticationSchemes.SsoCookie, options =>
            {
                options.Cookie.Path = "/admin";
            });
        }

        // Services
        services.AddBaseServices(globalSettings);
        services.AddDefaultServices(globalSettings);
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddSingleton<IGroupClaimRoleMapper>(_ => new GroupClaimRoleMapper(adminSettings));
        services.AddSingleton<ISsoSignInPrincipalBuilder, SsoSignInPrincipalBuilder>();
        services.AddDistributedCache(globalSettings);
        services.AddBillingOperations();
        services.AddHttpClient();
        services.AddHttpClient(HomeController.ExternalHttpClientName).AddSsrfProtection();

#if OSS
        services.AddOosServices();
#else
        services.AddCommercialCoreServices();
        services.AddSecretsManagerEfRepositories();
#endif

        // Mvc
        services.AddMvc(config =>
        {
            config.Filters.Add(new LoggingExceptionHandlerFilterAttribute());
        });
        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        services.Configure<RazorViewEngineOptions>(o =>
         {
             o.ViewLocationFormats.Add("/Auth/Views/{1}/{0}.cshtml");
             o.ViewLocationFormats.Add("/AdminConsole/Views/{1}/{0}.cshtml");
             o.ViewLocationFormats.Add("/Billing/Views/{1}/{0}.cshtml");
         });

        // Jobs service
        Jobs.JobsHostedService.AddJobsServices(services, globalSettings.SelfHosted);
        services.AddHostedService<Jobs.JobsHostedService>();
        if (globalSettings.SelfHosted)
        {
            services.AddHostedService<HostedServices.DatabaseMigrationHostedService>();
        }
        else
        {
            if (CoreHelpers.SettingHasValue(globalSettings.Mail.ConnectionString))
            {
                services.AddHostedService<HostedServices.AzureQueueMailHostedService>();
            }
        }
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        GlobalSettings globalSettings)
    {
        // Add general security headers
        app.UseMiddleware<SecurityHeadersMiddleware>();

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
        // Gates endpoints carrying IFeatureMetadata; required in any app that
        // routes requests through endpoints tagged with [RequireFeature].
        app.UseFeatureFlagChecks();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDefaultControllerRoute();
            endpoints.MapVersionEndpoint();
        });
    }
}
