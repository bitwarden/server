using Bit.Core.Billing.Organizations.Models;
using Stripe;
using Stripe.TestHelpers;
using Xunit;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Test.Billing.Organizations.Models;

public class TrialExtensionPolicyTests
{
    private static Subscription CreateTrialingSubscription(double remainingDays, string? scheduleId = null)
    {
        var now = DateTime.UtcNow;
        return new Subscription
        {
            Status = SubscriptionStatus.Trialing,
            TrialEnd = now.AddDays(remainingDays),
            ScheduleId = scheduleId,
            TestClock = new TestClock { FrozenTime = now }
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
}
