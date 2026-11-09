using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Stripe.TestHelpers;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Queries;

using static StripeConstants;

[SutProviderCustomize]
public class GetOrganizationTrialQueryTests
{
    private static Subscription CreateTrialingSubscription(
        DateTime now,
        double remainingDays,
        string? scheduleId = null) =>
        new()
        {
            Id = "sub_1",
            Status = SubscriptionStatus.Trialing,
            TrialEnd = now.AddDays(remainingDays),
            ScheduleId = scheduleId,
            TestClock = new TestClock { FrozenTime = now }
        };

    private static void StubSubscription(
        SutProvider<GetOrganizationTrialQuery> sutProvider,
        Organization organization,
        Subscription subscription) =>
        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(
                organization.GatewaySubscriptionId,
                Arg.Is<SubscriptionGetOptions>(options => options.Expand.Contains("test_clock")))
            .Returns(subscription);

    [Theory, BitAutoData]
    public async Task Run_NoGatewaySubscriptionId_ReturnsNullWithoutCallingStripe(
        Organization organization,
        SutProvider<GetOrganizationTrialQuery> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        var result = await sutProvider.Sut.Run(organization);

        Assert.Null(result);
        await sutProvider.GetDependency<IStripeAdapter>()
            .DidNotReceiveWithAnyArgs()
            .GetSubscriptionAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task Run_SubscriptionMissingInStripe_ReturnsNull(
        Organization organization,
        SutProvider<GetOrganizationTrialQuery> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new StripeException
            {
                StripeError = new StripeError { Code = ErrorCodes.ResourceMissing }
            });

        var result = await sutProvider.Sut.Run(organization);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task Run_SubscriptionNotTrialing_ReturnsNull(
        Organization organization,
        SutProvider<GetOrganizationTrialQuery> sutProvider)
    {
        var subscription = CreateTrialingSubscription(DateTime.UtcNow, 10);
        subscription.Status = SubscriptionStatus.Active;
        StubSubscription(sutProvider, organization, subscription);

        var result = await sutProvider.Sut.Run(organization);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task Run_TrialingWithoutTrialEnd_ReturnsNull(
        Organization organization,
        SutProvider<GetOrganizationTrialQuery> sutProvider)
    {
        var subscription = CreateTrialingSubscription(DateTime.UtcNow, 10);
        subscription.TrialEnd = null;
        StubSubscription(sutProvider, organization, subscription);

        var result = await sutProvider.Sut.Run(organization);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task Run_TrialingAndEligible_ReturnsExtendableTrial(
        Organization organization,
        SutProvider<GetOrganizationTrialQuery> sutProvider)
    {
        var subscription = CreateTrialingSubscription(DateTime.UtcNow, 10);
        StubSubscription(sutProvider, organization, subscription);

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(subscription.TrialEnd, result.TrialEnd);
        Assert.Null(result.ExtensionBlockedReason);
        Assert.True(result.CanExtend);
    }

    [Theory, BitAutoData]
    public async Task Run_TrialHasThirtyOrMoreDaysRemaining_ReturnsTrialWithBlockedReason(
        Organization organization,
        SutProvider<GetOrganizationTrialQuery> sutProvider)
    {
        var subscription = CreateTrialingSubscription(DateTime.UtcNow, 30);
        StubSubscription(sutProvider, organization, subscription);

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(subscription.TrialEnd, result.TrialEnd);
        Assert.Equal(TrialExtensionPolicy.TooManyDaysRemainingMessage, result.ExtensionBlockedReason);
        Assert.False(result.CanExtend);
    }

    [Theory, BitAutoData]
    public async Task Run_SubscriptionHasSchedule_ReturnsTrialWithBlockedReason(
        Organization organization,
        SutProvider<GetOrganizationTrialQuery> sutProvider)
    {
        var subscription = CreateTrialingSubscription(DateTime.UtcNow, 10, scheduleId: "sub_sched_1");
        StubSubscription(sutProvider, organization, subscription);

        var result = await sutProvider.Sut.Run(organization);

        Assert.NotNull(result);
        Assert.Equal(TrialExtensionPolicy.ScheduleAttachedMessage, result.ExtensionBlockedReason);
        Assert.False(result.CanExtend);
    }

    [Theory, BitAutoData]
    public async Task Run_StripeUnavailable_Propagates(
        Organization organization,
        SutProvider<GetOrganizationTrialQuery> sutProvider)
    {
        sutProvider.GetDependency<IStripeAdapter>()
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .ThrowsAsync(new StripeException { StripeError = new StripeError { Code = "api_error" } });

        await Assert.ThrowsAsync<StripeException>(() => sutProvider.Sut.Run(organization));
    }
}
