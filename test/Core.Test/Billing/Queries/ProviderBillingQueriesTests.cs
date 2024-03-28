using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Queries;
using Bit.Core.Billing.Queries.Implementations;
using Bit.Core.Billing.Repositories;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Queries;

[SutProviderCustomize]
public class ProviderBillingQueriesTests
{
    #region GetSubscriptionData

    [Theory, BitAutoData]
    public async Task GetSubscriptionData_NullProvider_ReturnsNull(
        SutProvider<ProviderBillingQueries> sutProvider,
        Guid providerId)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();

        providerRepository.GetByIdAsync(providerId).ReturnsNull();

        var subscriptionData = await sutProvider.Sut.GetSubscriptionData(providerId);

        Assert.Null(subscriptionData);

        await providerRepository.Received(1).GetByIdAsync(providerId);
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionData_NullSubscription_ReturnsNull(
        SutProvider<ProviderBillingQueries> sutProvider,
        Guid providerId,
        Provider provider)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();

        providerRepository.GetByIdAsync(providerId).Returns(provider);

        var subscriberQueries = sutProvider.GetDependency<ISubscriberQueries>();

        subscriberQueries.GetSubscription(provider).ReturnsNull();

        var subscriptionData = await sutProvider.Sut.GetSubscriptionData(providerId);

        Assert.Null(subscriptionData);

        await providerRepository.Received(1).GetByIdAsync(providerId);

        await subscriberQueries.Received(1).GetSubscription(
            provider,
            Arg.Is<SubscriptionGetOptions>(
                options => options.Expand.Count == 1 && options.Expand.First() == "customer"));
    }

    [Theory, BitAutoData]
    public async Task GetSubscriptionData_Success(
        SutProvider<ProviderBillingQueries> sutProvider,
        Guid providerId,
        Provider provider)
    {
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();

        providerRepository.GetByIdAsync(providerId).Returns(provider);

        var subscriberQueries = sutProvider.GetDependency<ISubscriberQueries>();

        var subscription = new Subscription();

        subscriberQueries.GetSubscription(provider, Arg.Is<SubscriptionGetOptions>(
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

        var subscriptionData = await sutProvider.Sut.GetSubscriptionData(providerId);

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

        await subscriberQueries.Received(1).GetSubscription(
            provider,
            Arg.Is<SubscriptionGetOptions>(
                options => options.Expand.Count == 1 && options.Expand.First() == "customer"));

        await providerPlanRepository.Received(1).GetByProviderId(providerId);

        return;

        void Compare(ProviderPlan providerPlan, ConfiguredProviderPlan configuredProviderPlan)
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
