﻿using Bit.Core.Billing.Caches;
using Bit.Core.Billing.Caches.Implementations;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;
using Bit.Core.Billing.Services.Implementations.AutomaticTax;

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
        services.AddKeyedTransient<IAutomaticTaxStrategy, PersonalUseAutomaticTaxStrategy>(AutomaticTaxFactory.PersonalUse);
        services.AddKeyedTransient<IAutomaticTaxStrategy, BusinessUseAutomaticTaxStrategy>(AutomaticTaxFactory.BusinessUse);
        services.AddTransient<IAutomaticTaxFactory, AutomaticTaxFactory>();
        services.AddLicenseServices();
        services.AddPricingClient();
    }
}
