using Bit.Core.Billing.Organizations.Models;
using Stripe;
using Stripe.TestHelpers;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Organizations.Models;

public class TrialExtensionPolicyTests
{
    private static Subscription CreateTrialingSubscription(
        double remainingDays,
        string? scheduleId = null,
        bool withTestClock = true)
    {
        var now = DateTime.UtcNow;
        return new Subscription
        {
            Status = SubscriptionStatus.Trialing,
            TrialEnd = now.AddDays(remainingDays),
            ScheduleId = scheduleId,
            TestClock = withTestClock ? new TestClock { FrozenTime = now } : null
        };
    }

    [Fact]
    public void IsEligible_NullSubscription_ReturnsFalse() =>
        Assert.False(TrialExtensionPolicy.IsEligible(null));

    [Theory]
    [InlineData(SubscriptionStatus.Active)]
    [InlineData(SubscriptionStatus.PastDue)]
    [InlineData(SubscriptionStatus.Canceled)]
    public void IsEligible_NotTrialing_ReturnsFalse(string status)
    {
        var subscription = CreateTrialingSubscription(10);
        subscription.Status = status;

        Assert.False(TrialExtensionPolicy.IsEligible(subscription));
    }

    [Fact]
    public void IsEligible_MissingTrialEnd_ReturnsFalse()
    {
        var subscription = CreateTrialingSubscription(10);
        subscription.TrialEnd = null;

        Assert.False(TrialExtensionPolicy.IsEligible(subscription));
    }

    [Fact]
    public void IsEligible_TwentyNineDaysRemaining_ReturnsTrue() =>
        Assert.True(TrialExtensionPolicy.IsEligible(CreateTrialingSubscription(29)));

    [Fact]
    public void IsEligible_ThirtyDaysRemaining_ReturnsFalse() =>
        Assert.False(TrialExtensionPolicy.IsEligible(CreateTrialingSubscription(30)));

    [Fact]
    public void IsEligible_ScheduleAttached_ReturnsFalse() =>
        Assert.False(TrialExtensionPolicy.IsEligible(CreateTrialingSubscription(10, scheduleId: "sub_sched_1")));

    [Fact]
    public void GetRemainingDays_PartialDay_RoundsUp() =>
        Assert.Equal(30, TrialExtensionPolicy.GetRemainingDays(CreateTrialingSubscription(29.2)));

    [Fact]
    public void ValidateDays_Null_ReturnsRequiredMessage() =>
        Assert.Equal(TrialExtensionPolicy.DaysRequiredMessage, TrialExtensionPolicy.ValidateDays(null));

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void ValidateDays_OutOfRange_ReturnsRangeMessage(int days) =>
        Assert.Equal(TrialExtensionPolicy.DaysOutOfRangeMessage, TrialExtensionPolicy.ValidateDays(days));

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void ValidateDays_InRange_ReturnsNull(int days) =>
        Assert.Null(TrialExtensionPolicy.ValidateDays(days));

    // Production subscriptions carry no test clock; these pin the wall-clock fallback. Half-day offsets keep the
    // ceiling stable even though DateTime.UtcNow is read again inside the policy.
    [Fact]
    public void GetRemainingDays_NoTestClock_UsesWallClock() =>
        Assert.Equal(10, TrialExtensionPolicy.GetRemainingDays(CreateTrialingSubscription(9.5, withTestClock: false)));

    [Fact]
    public void IsEligible_NoTestClock_UnderThirtyDays_ReturnsTrue() =>
        Assert.True(TrialExtensionPolicy.IsEligible(CreateTrialingSubscription(28.5, withTestClock: false)));

    [Fact]
    public void IsEligible_NoTestClock_ThirtyOrMoreDays_ReturnsFalse() =>
        Assert.False(TrialExtensionPolicy.IsEligible(CreateTrialingSubscription(29.5, withTestClock: false)));

    [Fact]
    public void GetIneligibilityReason_NullSubscription_ReturnsNoSubscriptionMessage() =>
        Assert.Equal(TrialExtensionPolicy.NoSubscriptionMessage, TrialExtensionPolicy.GetIneligibilityReason(null));

    [Fact]
    public void GetIneligibilityReason_NotTrialing_ReturnsNotTrialingMessage()
    {
        var subscription = CreateTrialingSubscription(10);
        subscription.Status = SubscriptionStatus.Active;

        Assert.Equal(TrialExtensionPolicy.NotTrialingMessage, TrialExtensionPolicy.GetIneligibilityReason(subscription));
    }

    [Fact]
    public void GetIneligibilityReason_MissingTrialEnd_ReturnsNotTrialingMessage()
    {
        var subscription = CreateTrialingSubscription(10);
        subscription.TrialEnd = null;

        Assert.Equal(TrialExtensionPolicy.NotTrialingMessage, TrialExtensionPolicy.GetIneligibilityReason(subscription));
    }

    [Fact]
    public void GetIneligibilityReason_ThirtyDaysRemaining_ReturnsTooManyDaysMessage() =>
        Assert.Equal(TrialExtensionPolicy.TooManyDaysRemainingMessage,
            TrialExtensionPolicy.GetIneligibilityReason(CreateTrialingSubscription(30)));

    [Fact]
    public void GetIneligibilityReason_ScheduleAttached_ReturnsScheduleMessage() =>
        Assert.Equal(TrialExtensionPolicy.ScheduleAttachedMessage,
            TrialExtensionPolicy.GetIneligibilityReason(CreateTrialingSubscription(10, scheduleId: "sub_sched_1")));

    [Fact]
    public void GetIneligibilityReason_Eligible_ReturnsNull() =>
        Assert.Null(TrialExtensionPolicy.GetIneligibilityReason(CreateTrialingSubscription(10)));
}
