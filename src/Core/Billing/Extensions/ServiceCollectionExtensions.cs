using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Caches.Implementations;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Billing.Payment;
using Bit.Core.Billing.Premium.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Core.Billing.Tax.Services;
using Bit.Core.Billing.Tax.Services.Implementations;

namespace Bit.Core.Billing.Extensions;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddBillingOperations(this IServiceCollection services)
    {
        services.AddSingleton<ITaxService, TaxService>();
        services.AddTransient<IOrganizationBillingService, OrganizationBillingService>();
        services.AddTransient<IPremiumUserBillingService, PremiumUserBillingService>();
        services.AddTransient<ISetupIntentCache, SetupIntentDistributedCache>();
        services.AddTransient<ISubscriberService, SubscriberService>();
        services.AddLicenseServices();
        services.AddPricingClient();
        services.AddPaymentOperations();
        services.AddOrganizationLicenseCommandsQueries();
        services.AddPremiumCommands();
        services.AddTransient<IGetOrganizationMetadataQuery, GetOrganizationMetadataQuery>();
        services.AddTransient<IGetOrganizationWarningsQuery, GetOrganizationWarningsQuery>();
        services.AddTransient<IRestartSubscriptionCommand, RestartSubscriptionCommand>();
        services.AddTransient<IPreviewOrganizationTaxCommand, PreviewOrganizationTaxCommand>();
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
    }
}
