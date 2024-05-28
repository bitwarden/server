using Bit.Api.Billing.Controllers;
using Bit.Api.Billing.Models.Responses;
using Bit.Core;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Api.Test.Billing.Controllers;

[ControllerCustomize(typeof(ProviderBillingController))]
[SutProviderCustomize]
public class ProviderBillingControllerTests
{
    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_FFDisabled_NotFound(
        Guid providerId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(false);

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NullProvider_NotFound(
        Guid providerId,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).ReturnsNull();

        var result = await sutProvider.Sut.GetSubscriptionAsync(providerId);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NotProviderAdmin_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(false);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_ProviderNotBillable_Unauthorized(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        provider.Type = ProviderType.Reseller;
        provider.Status = ProviderStatusType.Created;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(false);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_NullConsolidatedBillingSubscription_NotFound(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(provider, sutProvider);

        sutProvider.GetDependency<IProviderBillingService>().GetConsolidatedBillingSubscription(provider).ReturnsNull();

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<NotFound>(result);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionAsync_Ok(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        ConfigureStableInputs(provider, sutProvider);

        var configuredProviderPlans = new List<ConfiguredProviderPlanDTO>
        {
            new (Guid.NewGuid(), provider.Id, PlanType.TeamsMonthly, 50, 10, 30),
            new (Guid.NewGuid(), provider.Id , PlanType.EnterpriseMonthly, 100, 0, 90)
        };

        var subscription = new Subscription
        {
            Status = "active",
            CurrentPeriodEnd = new DateTime(2025, 1, 1),
            Customer = new Customer { Discount = new Discount { Coupon = new Coupon { PercentOff = 10 } } }
        };

        var consolidatedBillingSubscription = new ConsolidatedBillingSubscriptionDTO(
            configuredProviderPlans,
            subscription);

        sutProvider.GetDependency<IProviderBillingService>().GetConsolidatedBillingSubscription(provider)
            .Returns(consolidatedBillingSubscription);

        var result = await sutProvider.Sut.GetSubscriptionAsync(provider.Id);

        Assert.IsType<Ok<ConsolidatedBillingSubscriptionResponse>>(result);

        var response = ((Ok<ConsolidatedBillingSubscriptionResponse>)result).Value;

        Assert.Equal(response.Status, subscription.Status);
        Assert.Equal(response.CurrentPeriodEndDate, subscription.CurrentPeriodEnd);
        Assert.Equal(response.DiscountPercentage, subscription.Customer!.Discount!.Coupon!.PercentOff);

        var teamsPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var providerTeamsPlan = response.Plans.FirstOrDefault(plan => plan.PlanName == teamsPlan.Name);
        Assert.NotNull(providerTeamsPlan);
        Assert.Equal(50, providerTeamsPlan.SeatMinimum);
        Assert.Equal(10, providerTeamsPlan.PurchasedSeats);
        Assert.Equal(30, providerTeamsPlan.AssignedSeats);
        Assert.Equal(60 * teamsPlan.PasswordManager.SeatPrice, providerTeamsPlan.Cost);
        Assert.Equal("Monthly", providerTeamsPlan.Cadence);

        var enterprisePlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);
        var providerEnterprisePlan = response.Plans.FirstOrDefault(plan => plan.PlanName == enterprisePlan.Name);
        Assert.NotNull(providerEnterprisePlan);
        Assert.Equal(100, providerEnterprisePlan.SeatMinimum);
        Assert.Equal(0, providerEnterprisePlan.PurchasedSeats);
        Assert.Equal(90, providerEnterprisePlan.AssignedSeats);
        Assert.Equal(100 * enterprisePlan.PasswordManager.SeatPrice, providerEnterprisePlan.Cost);
        Assert.Equal("Monthly", providerEnterprisePlan.Cadence);
    }

    private static void ConfigureStableInputs(
        Provider provider,
        SutProvider<ProviderBillingController> sutProvider)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling)
            .Returns(true);

        provider.Type = ProviderType.Msp;
        provider.Status = ProviderStatusType.Billable;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(provider.Id).Returns(provider);

        sutProvider.GetDependency<ICurrentContext>().ProviderProviderAdmin(provider.Id)
            .Returns(true);
    }
}
