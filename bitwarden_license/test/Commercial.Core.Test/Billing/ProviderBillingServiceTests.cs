using Bit.Commercial.Core.Billing;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

using static Bit.Core.Test.Billing.Utilities;
namespace Bit.Commercial.Core.Test.Billing;

[SutProviderCustomize]
public class ProviderBillingServiceTests
{
    #region GetAssignedSeatTotalForPlanOrThrow
    [Theory, BitAutoData]
    public async Task GetAssignedSeatTotalForPlanOrThrow_NullProvider_ContactSupport(
        Guid providerId,
        SutProvider<ProviderBillingService> sutProvider)
        => await ThrowsContactSupportAsync(() =>
            sutProvider.Sut.GetAssignedSeatTotalForPlanOrThrow(providerId, PlanType.TeamsMonthly));

    [Theory, BitAutoData]
    public async Task GetAssignedSeatTotalForPlanOrThrow_ResellerProvider_ContactSupport(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        provider.Type = ProviderType.Reseller;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).Returns(provider);

        await ThrowsContactSupportAsync(
            () => sutProvider.Sut.GetAssignedSeatTotalForPlanOrThrow(providerId, PlanType.TeamsMonthly),
            internalMessage: "Consolidated billing does not support reseller-type providers");
    }

    [Theory, BitAutoData]
    public async Task GetAssignedSeatTotalForPlanOrThrow_Succeeds(
        Guid providerId,
        Provider provider,
        SutProvider<ProviderBillingService> sutProvider)
    {
        provider.Type = ProviderType.Msp;

        sutProvider.GetDependency<IProviderRepository>().GetByIdAsync(providerId).Returns(provider);

        var teamsMonthlyPlan = StaticStore.GetPlan(PlanType.TeamsMonthly);
        var enterpriseMonthlyPlan = StaticStore.GetPlan(PlanType.EnterpriseMonthly);

        var providerOrganizationOrganizationDetailList = new List<ProviderOrganizationOrganizationDetails>
        {
            new ()
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 10
            },
            new ()
            {
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 10
            },
            new ()
            {
                // Ignored because of status.
                Plan = teamsMonthlyPlan.Name,
                Status = OrganizationStatusType.Created,
                Seats = 100
            },
            new ()
            {
                // Ignored because of plan.
                Plan = enterpriseMonthlyPlan.Name,
                Status = OrganizationStatusType.Managed,
                Seats = 30
            }
        };

        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetManyDetailsByProviderAsync(providerId)
            .Returns(providerOrganizationOrganizationDetailList);

        var assignedSeatTotal = await sutProvider.Sut.GetAssignedSeatTotalForPlanOrThrow(providerId, PlanType.TeamsMonthly);

        Assert.Equal(20, assignedSeatTotal);
    }
    #endregion

    #region GetSubscriptionData
    [Theory, BitAutoData]
    public async Task GetSubscriptionData_NullProvider_ReturnsNull(
        SutProvider<ProviderBillingService> sutProvider,
        Guid providerId)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();

        providerRepository.GetByIdAsync(providerId).ReturnsNull();

        var subscriptionData = await sutProvider.Sut.GetSubscriptionDTO(providerId);

        Assert.Null(subscriptionData);

        await providerRepository.Received(1).GetByIdAsync(providerId);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionData_NullSubscription_ReturnsNull(
        SutProvider<ProviderBillingService> sutProvider,
        Guid providerId,
        Provider provider)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();

        providerRepository.GetByIdAsync(providerId).Returns(provider);

        var subscriberService = sutProvider.GetDependency<ISubscriberService>();

        subscriberService.GetSubscription(provider).ReturnsNull();

        var subscriptionData = await sutProvider.Sut.GetSubscriptionDTO(providerId);

        Assert.Null(subscriptionData);

        await providerRepository.Received(1).GetByIdAsync(providerId);

        await subscriberService.Received(1).GetSubscription(
            provider,
            Arg.Is<SubscriptionGetOptions>(
                options => options.Expand.Count == 1 && options.Expand.First() == "customer"));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionData_Success(
        SutProvider<ProviderBillingService> sutProvider,
        Guid providerId,
        Provider provider)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();

        providerRepository.GetByIdAsync(providerId).Returns(provider);

        var subscriberService = sutProvider.GetDependency<ISubscriberService>();

        var subscription = new Subscription();

        subscriberService.GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(
            options => options.Expand.Count == 1 && options.Expand.First() == "customer")).Returns(subscription);

        var providerPlanRepository = sutProvider.GetDependency<IProviderPlanRepository>();

        var enterprisePlan = new ProviderPlan
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            PlanType = PlanType.EnterpriseMonthly,
            SeatMinimum = 100,
            PurchasedSeats = 0,
            AllocatedSeats = 0
        };

        var teamsPlan = new ProviderPlan
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            PlanType = PlanType.TeamsMonthly,
            SeatMinimum = 50,
            PurchasedSeats = 10,
            AllocatedSeats = 60
        };

        var providerPlans = new List<ProviderPlan>
        {
            enterprisePlan,
            teamsPlan,
        };

        providerPlanRepository.GetByProviderId(providerId).Returns(providerPlans);

        var subscriptionData = await sutProvider.Sut.GetSubscriptionDTO(providerId);

        Assert.NotNull(subscriptionData);

        Assert.Equivalent(subscriptionData.Subscription, subscription);

        Assert.Equal(2, subscriptionData.ProviderPlans.Count);

        var configuredEnterprisePlan =
            subscriptionData.ProviderPlans.FirstOrDefault(configuredPlan =>
                configuredPlan.PlanType == PlanType.EnterpriseMonthly);

        var configuredTeamsPlan =
            subscriptionData.ProviderPlans.FirstOrDefault(configuredPlan =>
                configuredPlan.PlanType == PlanType.TeamsMonthly);

        Compare(enterprisePlan, configuredEnterprisePlan);

        Compare(teamsPlan, configuredTeamsPlan);

        await providerRepository.Received(1).GetByIdAsync(providerId);

        await subscriberService.Received(1).GetSubscription(
            provider,
            Arg.Is<SubscriptionGetOptions>(
                options => options.Expand.Count == 1 && options.Expand.First() == "customer"));

        await providerPlanRepository.Received(1).GetByProviderId(providerId);

        return;

        void Compare(ProviderPlan providerPlan, ConfiguredProviderPlanDTO configuredProviderPlan)
        {
            Assert.NotNull(configuredProviderPlan);
            Assert.Equal(providerPlan.Id, configuredProviderPlan.Id);
            Assert.Equal(providerPlan.ProviderId, configuredProviderPlan.ProviderId);
            Assert.Equal(providerPlan.SeatMinimum!.Value, configuredProviderPlan.SeatMinimum);
            Assert.Equal(providerPlan.PurchasedSeats!.Value, configuredProviderPlan.PurchasedSeats);
            Assert.Equal(providerPlan.AllocatedSeats!.Value, configuredProviderPlan.AssignedSeats);
        }
    }
    #endregion
}
