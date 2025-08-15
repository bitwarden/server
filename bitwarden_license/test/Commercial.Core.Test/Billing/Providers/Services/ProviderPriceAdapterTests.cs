using Bit.Commercial.Core.Billing.Providers.Services;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.Billing.Enums;
using Stripe;
using Xunit;

namespace Bit.Commercial.Core.Test.Billing.Providers;

public class ProviderPriceAdapterTests
{
    [Theory]
    [InlineData("password-manager-provider-portal-enterprise-monthly-2024", PlanType.EnterpriseMonthly)]
    [InlineData("password-manager-provider-portal-teams-monthly-2024", PlanType.TeamsMonthly)]
    public void GetPriceId_MSP_Legacy_Succeeds(string priceId, PlanType planType)
    {
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Type = ProviderType.Msp
        };

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = priceId } }
                ]
            }
        };

        var result = ProviderPriceAdapter.GetPriceId(provider, subscription, planType);

        Assert.Equal(result, priceId);
    }

    [Theory]
    [InlineData("provider-portal-enterprise-monthly-2025", PlanType.EnterpriseMonthly)]
    [InlineData("provider-portal-teams-monthly-2025", PlanType.TeamsMonthly)]
    public void GetPriceId_MSP_Active_Succeeds(string priceId, PlanType planType)
    {
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Type = ProviderType.Msp
        };

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = priceId } }
                ]
            }
        };

        var result = ProviderPriceAdapter.GetPriceId(provider, subscription, planType);

        Assert.Equal(result, priceId);
    }

    [Theory]
    [InlineData("password-manager-provider-portal-enterprise-annually-2024", PlanType.EnterpriseAnnually)]
    [InlineData("password-manager-provider-portal-enterprise-monthly-2024", PlanType.EnterpriseMonthly)]
    public void GetPriceId_BusinessUnit_Legacy_Succeeds(string priceId, PlanType planType)
    {
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Type = ProviderType.BusinessUnit
        };

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = priceId } }
                ]
            }
        };

        var result = ProviderPriceAdapter.GetPriceId(provider, subscription, planType);

        Assert.Equal(result, priceId);
    }

    [Theory]
    [InlineData("business-unit-portal-enterprise-annually-2025", PlanType.EnterpriseAnnually)]
    [InlineData("business-unit-portal-enterprise-monthly-2025", PlanType.EnterpriseMonthly)]
    public void GetPriceId_BusinessUnit_Active_Succeeds(string priceId, PlanType planType)
    {
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Type = ProviderType.BusinessUnit
        };

        var subscription = new Subscription
        {
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem { Price = new Price { Id = priceId } }
                ]
            }
        };

        var result = ProviderPriceAdapter.GetPriceId(provider, subscription, planType);

        Assert.Equal(result, priceId);
    }

    [Theory]
    [InlineData("provider-portal-enterprise-monthly-2025", PlanType.EnterpriseMonthly)]
    [InlineData("provider-portal-teams-monthly-2025", PlanType.TeamsMonthly)]
    public void GetActivePriceId_MSP_Succeeds(string priceId, PlanType planType)
    {
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Type = ProviderType.Msp
        };

        var result = ProviderPriceAdapter.GetActivePriceId(provider, planType);

        Assert.Equal(result, priceId);
    }

    [Theory]
    [InlineData("business-unit-portal-enterprise-annually-2025", PlanType.EnterpriseAnnually)]
    [InlineData("business-unit-portal-enterprise-monthly-2025", PlanType.EnterpriseMonthly)]
    public void GetActivePriceId_BusinessUnit_Succeeds(string priceId, PlanType planType)
    {
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Type = ProviderType.BusinessUnit
        };

        var result = ProviderPriceAdapter.GetActivePriceId(provider, planType);

        Assert.Equal(result, priceId);
    }
}
