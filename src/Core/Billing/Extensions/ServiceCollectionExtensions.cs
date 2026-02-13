using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Caches.Implementations;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Payment;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Premium.Queries;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Billing.Subscriptions.Queries;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Billing.Tax.Services.Implementations;
using Bit.Core.Services;
using Bit.Core.Services.Implementations;

namespace Bit.Core.Billing.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddBillingOperations(this IServiceCollection services)
    {
        services.AddSingleton<ITaxService, TaxService>();
        services.AddTransient<IOrganizationBillingService, OrganizationBillingService>();
        services.AddTransient<IPremiumUserBillingService, PremiumUserBillingService>();
        services.AddTransient<ISetupIntentCache, SetupIntentDistributedCache>();
        services.AddTransient<ISubscriberService, SubscriberService>();
        services.TryAddTransient<ISubscriptionDiscountService, SubscriptionDiscountService>();
        services.AddLicenseServices();
        services.AddLicenseOperations();
        services.AddPricingClient();
        services.AddPaymentOperations();
        services.AddOrganizationLicenseCommandsQueries();
        services.AddPremiumCommands();
        services.AddPremiumQueries();
        services.AddTransient<IGetOrganizationMetadataQuery, GetOrganizationMetadataQuery>();
        services.AddTransient<IGetOrganizationWarningsQuery, GetOrganizationWarningsQuery>();
        services.AddTransient<IRestartSubscriptionCommand, RestartSubscriptionCommand>();
        services.AddTransient<IPreviewOrganizationTaxCommand, PreviewOrganizationTaxCommand>();
        services.AddTransient<IGetBitwardenSubscriptionQuery, GetBitwardenSubscriptionQuery>();
        services.AddTransient<IReinstateSubscriptionCommand, ReinstateSubscriptionCommand>();
        services.AddTransient<IBraintreeService, BraintreeService>();
    }

    private static void AddOrganizationLicenseCommandsQueries(this IServiceCollection services)
    {
        services.AddScoped<IGetCloudOrganizationLicenseQuery, GetCloudOrganizationLicenseQuery>();
        services.AddScoped<IGetSelfHostedOrganizationLicenseQuery, GetSelfHostedOrganizationLicenseQuery>();
        services.AddScoped<IUpdateOrganizationLicenseCommand, UpdateOrganizationLicenseCommand>();
    }

    private static void AddPremiumCommands(this IServiceCollection services)
    {
        services.AddScoped<ICreatePremiumCloudHostedSubscriptionCommand, CreatePremiumCloudHostedSubscriptionCommand>();
        services.AddScoped<ICreatePremiumSelfHostedSubscriptionCommand, CreatePremiumSelfHostedSubscriptionCommand>();
        services.AddTransient<IPreviewPremiumTaxCommand, PreviewPremiumTaxCommand>();
        services.AddScoped<IPreviewPremiumUpgradeProrationCommand, PreviewPremiumUpgradeProrationCommand>();
        services.AddScoped<IUpdatePremiumStorageCommand, UpdatePremiumStorageCommand>();
        services.AddScoped<IUpgradePremiumToOrganizationCommand, UpgradePremiumToOrganizationCommand>();
    }

    private static void AddPremiumQueries(this IServiceCollection services)
    {
        services.AddScoped<IHasPremiumAccessQuery, HasPremiumAccessQuery>();
    }
}
