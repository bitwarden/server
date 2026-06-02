using System.Globalization;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Seeder.Services;
using Bit.SeederApi.Extensions;
using Bit.SeederApi.Utilities;
using Bit.SharedWeb.Utilities;
using Braintree;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.SeederApi;

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
        services.AddOptions();

        var globalSettings = services.AddGlobalSettingsServices(Configuration, Environment);

        services.AddCustomDataProtectionServices(Environment, globalSettings);

        services.AddTokenizers();
        services.AddDatabaseRepositories(globalSettings);
        services.AddTestPlayIdTracking(globalSettings);
        services.AddManglerService(globalSettings);

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        services.AddScoped<IPasswordHasher<Core.Entities.User>, PasswordHasher<Core.Entities.User>>();

        services.AddSeederApiServices();
        services.AddScenes();
        services.AddQueries();

        services.TryAddSingleton<IGlobalSettings>(globalSettings);
        AddBillingServices(services, globalSettings);

        services.Configure<SeederSettings>(Configuration.GetSection("seederSettings"));

        services.AddAuthentication(BasicAuthenticationOptions.DefaultScheme)
            .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
                BasicAuthenticationOptions.DefaultScheme, null);

        services.AddAuthorization();

        services.AddControllers();

        Jobs.JobsHostedService.AddJobsServices(services);
        services.AddHostedService<Jobs.JobsHostedService>();
    }

    /// <summary>
    /// Registers billing-related services so that <c>FinalizeOrganizationBillingStep</c> can
    /// run inside any scene that exercises the org pipeline. Uses a no-op feature service so
    /// the seeder doesn't depend on LaunchDarkly.
    /// </summary>
    private static void AddBillingServices(IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddHttpClient();
        services.AddSingleton<IStripeAdapter, StripeAdapter>();
        services.AddSingleton<IBraintreeGateway>(_ => new BraintreeGateway
        {
            Environment = globalSettings.Braintree.Production
                ? Braintree.Environment.PRODUCTION
                : Braintree.Environment.SANDBOX,
            MerchantId = globalSettings.Braintree.MerchantId,
            PublicKey = globalSettings.Braintree.PublicKey,
            PrivateKey = globalSettings.Braintree.PrivateKey,
        });
        services.AddScoped<IFeatureService, NoOpFeatureService>();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddBillingOperations();

        if (!string.IsNullOrWhiteSpace(globalSettings.Stripe?.ApiKey))
        {
            Stripe.StripeConfiguration.ApiKey = globalSettings.Stripe.ApiKey;
            Stripe.StripeConfiguration.MaxNetworkRetries = globalSettings.Stripe.MaxNetworkRetries;
        }
    }

    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        IHostApplicationLifetime appLifetime,
        GlobalSettings globalSettings)
    {
        if (env.IsProduction())
        {
            throw new InvalidOperationException(
                "SeederApi cannot be run in production environments. This service is intended for test data generation only.");
        }

        if (globalSettings.TestPlayIdTrackingEnabled)
        {
            app.UseMiddleware<PlayIdMiddleware>();
        }

        if (!env.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(name: "default", pattern: "{controller=Seed}/{action=Index}/{id?}");
        });
    }
}
