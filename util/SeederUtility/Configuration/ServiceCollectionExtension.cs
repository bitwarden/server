using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Bit.SharedWeb.Utilities;
using Braintree;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.SeederUtility.Configuration;

public static class ServiceCollectionExtension
{
    public static void ConfigureServices(ServiceCollection services, bool enableMangling = false)
    {
        var globalSettings = GlobalSettingsFactory.GlobalSettings;

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddFilter("Microsoft.EntityFrameworkCore.Model.Validation", LogLevel.Error);
        });
        services.AddSingleton(globalSettings);
        services.AddSingleton<IGlobalSettings, GlobalSettings>(_ => globalSettings);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.TryAddSingleton<ISeedReader, SeedReader>();

        var dpBuilder = services.AddDataProtection().SetApplicationName("Bitwarden");
        // Persist DataProtection keys to a shared directory when configured, so
        // records this tool encrypts are decryptable by the running app. Needed
        // when seeding an instance whose components share a DataProtection key ring
        // (e.g. self-host). No-op when the directory isn't set.
        if (!string.IsNullOrWhiteSpace(globalSettings.DataProtection.Directory))
        {
            dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(globalSettings.DataProtection.Directory));
        }

        services.AddAttachmentStorageService(globalSettings);

        services.AddDatabaseRepositories(globalSettings);

        if (enableMangling)
        {
            services.TryAddScoped<IManglerService, ManglerService>();
        }
        else
        {
            services.TryAddSingleton<IManglerService, NoOpManglerService>();
        }

        services.TryAddSingleton<IWebHostEnvironment>(new DevelopmentWebHostEnvironment());
        services.AddLicenseServices();
        services.TryAddSingleton<IMailService, NoopMailService>();
        services.AddPush(globalSettings);
        services.TryAddSingleton<ILicensingService, LicensingService>();
        services.TryAddSingleton<ISeederLicenseSigner, SeederLicenseSigner>();

        AddStripeBilling(services, globalSettings);
    }

    /// <summary>
    /// Closes the DI gaps the Core billing graph needs, so <c>--stripe-billing</c> can drive the same
    /// <c>IOrganizationBillingService</c> the API uses. Registration is unconditional and inert: nothing here
    /// contacts Stripe until a seed actually opts in.
    /// </summary>
    private static void AddStripeBilling(ServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddBillingOperations();

        // Deliberately duplicates a subset of SharedWeb's AddDefaultServices registration rather than
        // extracting a shared helper — the Seeder must never require a change to shared/production code for
        // its own DI needs. The Add*/TryAdd* mix below is harmless: nothing else in this composition chain
        // (AddDatabaseRepositories, AddLicenseServices, AddPush, AddBillingOperations) registers either type.
        services.AddSingleton<IStripeAdapter, StripeAdapter>();

        // Constructed but never exercised — the seeder only ever pays by card — so empty credentials are fine.
        services.AddSingleton<IBraintreeGateway>(_ => new BraintreeGateway
        {
            Environment = globalSettings.Braintree.Production
                ? Braintree.Environment.PRODUCTION
                : Braintree.Environment.SANDBOX,
            MerchantId = globalSettings.Braintree.MerchantId,
            PublicKey = globalSettings.Braintree.PublicKey,
            PrivateKey = globalSettings.Braintree.PrivateKey,
        });

        // SubscriberService → PriceIncreaseScheduler has a hard constructor dependency on the obsolete
        // IFeatureService. NoopFeatureService satisfies it without pulling in the LaunchDarkly-backed SDK.
        services.TryAddSingleton<Bit.Core.Services.IFeatureService, NoopFeatureService>();

        services.AddScoped<IStripeBillingInitializer, StripeBillingInitializer>();

        // StripeAdapter reads these statics rather than GlobalSettings, mirroring src/Api/Startup.cs.
        // Left unarmed when no key is configured, or when the configured key isn't a test-mode key, so a
        // default seed cannot half-initialize the Stripe client and this tool can never hold a live key.
        var stripeSettings = globalSettings.Stripe;
        if (!string.IsNullOrWhiteSpace(stripeSettings?.ApiKey) &&
            stripeSettings.ApiKey.StartsWith(StripeBillingInitializer.TestKeyPrefix, StringComparison.Ordinal))
        {
            StripeConfiguration.ApiKey = stripeSettings.ApiKey;
            StripeConfiguration.MaxNetworkRetries = stripeSettings.MaxNetworkRetries;
        }
    }
}

internal sealed class DevelopmentWebHostEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ApplicationName { get; set; } = "SeederUtility";
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public string EnvironmentName { get; set; } = Environments.Development;
}
