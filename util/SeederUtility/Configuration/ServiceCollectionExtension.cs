using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Seeder.Services;
using Bit.SharedWeb.Utilities;
using Braintree;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
        services.AddSingleton<Bit.Core.Settings.IGlobalSettings>(globalSettings);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.TryAddSingleton<ISeedReader, SeedReader>();

        services.AddDataProtection().SetApplicationName("Bitwarden");

        services.AddDatabaseRepositories(globalSettings);

        if (enableMangling)
        {
            services.TryAddScoped<IManglerService, ManglerService>();
        }
        else
        {
            services.TryAddSingleton<IManglerService, NoOpManglerService>();
        }

        AddBillingServices(services, globalSettings);
    }

    /// <summary>
    /// Registers the billing-related services needed by <c>FinalizeOrganizationBillingStep</c>.
    /// Pulls the production billing operations and a minimal set of payment-platform adapters,
    /// plus a no-op feature service so we don't depend on LaunchDarkly for seeded runs.
    /// </summary>
    private static void AddBillingServices(IServiceCollection services, Bit.Core.Settings.GlobalSettings globalSettings)
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
}
