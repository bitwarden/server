using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.Subscriptions.Schedules;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Billing.Tax.Services.Implementations;
using Bit.Core.Settings;
using Bit.Seeder.Services;
using Braintree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Seeder.Extensions;

/// <summary>
/// Registers the Seeder's Stripe billing services for both seeder hosts, the CLI and the SeederApi.
/// </summary>
public static class StripeBillingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IOrganizationStripeBillingService"/>, <see cref="IUserStripeBillingService"/>, and
    /// the Core billing services they construct from. Registration is inert: nothing here contacts Stripe until
    /// a seed opts in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers the constructor closure of the two Seeder services and nothing more, rather than calling
    /// <c>AddBillingOperations()</c>. The SeederApi validates every registration at startup in Development,
    /// and <c>AddBillingOperations()</c> registers commands whose dependencies (such as <c>IUserService</c>) no
    /// seeder host provides. The closure is <c>OrganizationBillingService</c>, <c>SubscriberService</c>,
    /// <c>HasPaymentMethodQuery</c>, <c>SubscriptionDiscountService</c> and its audience filters,
    /// <c>PriceIncreaseScheduler</c>, <c>SubscriptionScheduleCreator</c>, <c>TaxService</c>, the pricing client,
    /// the Stripe adapter, the Braintree gateway, and the organization ability cache, which the hosts'
    /// <c>LicensingService</c> also uses. Repositories, <see cref="GlobalSettings"/>, and logging come from the
    /// host. When one of these Core constructors gains a dependency, add it here.
    /// </para>
    /// <para>
    /// Lifetimes match Core's own registrations. The Seeder never changes production code for its own DI
    /// needs, so this duplicates a subset of Core's and SharedWeb's registrations instead of sharing them.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddStripeBilling(this IServiceCollection services, GlobalSettings globalSettings)
    {
        services.AddPricingClient();
        services.AddOrganizationAbilityCache(globalSettings);
        services.AddSingleton<ITaxService, TaxService>();
        services.AddTransient<ISubscriptionScheduleCreator, SubscriptionScheduleCreator>();
        services.AddTransient<IPriceIncreaseScheduler, PriceIncreaseScheduler>();
        services.AddTransient<ISubscriberService, SubscriberService>();
        services.AddTransient<IHasPaymentMethodQuery, HasPaymentMethodQuery>();
        services.AddScoped<IDiscountAudienceFilter, AllUsersFilter>();
        services.AddScoped<IDiscountAudienceFilter, UserHasNoPreviousSubscriptionsFilter>();
        services.AddScoped<IDiscountAudienceFilterFactory, DiscountAudienceFilterFactory>();
        services.AddTransient<ISubscriptionDiscountService, SubscriptionDiscountService>();
        services.AddTransient<IOrganizationBillingService, OrganizationBillingService>();

        services.AddSingleton<IStripeAdapter, StripeAdapter>();

        // The Seeder only pays by card, so nothing calls this gateway and empty credentials are enough.
        services.AddSingleton<IBraintreeGateway>(_ => new BraintreeGateway
        {
            Environment = globalSettings.Braintree.Production
                ? Braintree.Environment.PRODUCTION
                : Braintree.Environment.SANDBOX,
            MerchantId = globalSettings.Braintree.MerchantId,
            PublicKey = globalSettings.Braintree.PublicKey,
            PrivateKey = globalSettings.Braintree.PrivateKey,
        });

        // The SeederApi already has the SDK feature service from UseBitwardenSdk(), so this only takes effect
        // in the CLI, where NoopFeatureService keeps the LaunchDarkly-backed SDK out.
        services.TryAddSingleton<Bitwarden.Server.Sdk.Features.IFeatureService, NoopFeatureService>();

        services.AddScoped<IOrganizationStripeBillingService, OrganizationStripeBillingService>();
        services.AddScoped<IUserStripeBillingService, UserStripeBillingService>();

        // StripeAdapter reads these statics rather than GlobalSettings, mirroring src/Api/Startup.cs.
        // Left unarmed when no key is configured, or when the configured key isn't a test-mode key, so a
        // default seed cannot half-initialize the Stripe client and the client is never given a live key.
        var stripeSettings = globalSettings.Stripe;
        if (!string.IsNullOrWhiteSpace(stripeSettings?.ApiKey) &&
            stripeSettings.ApiKey.StartsWith(StripeSettingValidator.TestKeyPrefix, StringComparison.Ordinal))
        {
            Stripe.StripeConfiguration.ApiKey = stripeSettings.ApiKey;
            Stripe.StripeConfiguration.MaxNetworkRetries = stripeSettings.MaxNetworkRetries;
        }

        return services;
    }
}
