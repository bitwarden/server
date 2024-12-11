using System.Globalization;
using Bit.Admin.IdentityServer;
using Bit.Admin.Services;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Migration;
using Bit.Core.Context;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stripe;
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
        services.Configure<AdminSettings>(Configuration.GetSection("AdminSettings"));

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

        // Context
        services.AddScoped<ICurrentContext, CurrentContext>();
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

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
        services.AddBaseServices(globalSettings);
        services.AddDefaultServices(globalSettings);
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddDistributedCache(globalSettings);
        services.AddBillingOperations();
        services.AddHttpClient();
        services.AddProviderMigration();

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
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings
    )
    {
        app.UseSerilog(env, appLifetime, globalSettings);

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
        app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());
    }
}
