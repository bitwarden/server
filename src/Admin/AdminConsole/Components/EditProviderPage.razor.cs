using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Services;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Components;

namespace Bit.Admin.AdminConsole.Components;

public partial class EditProviderPage(
    GlobalSettings globalSettings,
    IAccessControlService accessControlService,
    IFeatureService featureService,
    IProviderRepository providerRepository,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IProviderUserRepository providerUserRepository,
    IWebHostEnvironment webHostEnvironment)
    : ComponentBase
{
    private readonly string _stripeUrl = webHostEnvironment.GetStripeUrl();
    private readonly string _braintreeMerchantUrl = webHostEnvironment.GetBraintreeMerchantUrl();
    private readonly string _braintreeMerchantId = globalSettings.Braintree.MerchantId;

    [Parameter] public Guid Id { get; set; }

    public ProviderEditModel? Model { get; set; }

    public ProviderViewModel? ViewModel { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var provider = await providerRepository.GetByIdAsync(Id);

        if (provider == null)
        {
            return;
        }

        var users = await providerUserRepository.GetManyDetailsByProviderAsync(Id);
        var providerOrganizations = await providerOrganizationRepository.GetManyDetailsByProviderAsync(Id);

        var isConsolidatedBillingEnabled = featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling);

        if (!isConsolidatedBillingEnabled || !provider.IsBillable())
        {
            Model = new ProviderEditModel(provider, users, providerOrganizations, new List<ProviderPlan>());
            ViewModel = new ProviderViewModel(provider, users, providerOrganizations);
            return;
        }

        var providerPlans = await providerPlanRepository.GetByProviderId(Id);

        Model = new ProviderEditModel(
            provider, users, providerOrganizations,
            providerPlans.ToList(), GetGatewayCustomerUrl(provider), GetGatewaySubscriptionUrl(provider));

        ViewModel = new ProviderViewModel(provider, users, providerOrganizations);
    }

    private string GetGatewayCustomerUrl(Provider provider)
    {
        if (!provider.Gateway.HasValue || string.IsNullOrEmpty(provider.GatewayCustomerId))
        {
            return null;
        }

        return provider.Gateway switch
        {
            GatewayType.Stripe => $"{_stripeUrl}/customers/{provider.GatewayCustomerId}",
            GatewayType.PayPal => $"{_braintreeMerchantUrl}/{_braintreeMerchantId}/${provider.GatewayCustomerId}",
            _ => null
        };
    }

    private string GetGatewaySubscriptionUrl(Provider provider)
    {
        if (!provider.Gateway.HasValue || string.IsNullOrEmpty(provider.GatewaySubscriptionId))
        {
            return null;
        }

        return provider.Gateway switch
        {
            GatewayType.Stripe => $"{_stripeUrl}/subscriptions/{provider.GatewaySubscriptionId}",
            GatewayType.PayPal => $"{_braintreeMerchantUrl}/{_braintreeMerchantId}/subscriptions/${provider.GatewaySubscriptionId}",
            _ => null
        };
    }
}

