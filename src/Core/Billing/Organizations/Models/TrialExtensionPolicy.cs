using Bit.Core.Billing.Constants;
using Stripe;

namespace Bit.Core.Billing.Organizations.Models;

/// <summary>
/// Rules governing when and by how much an admin can extend an organization's Stripe trial.
/// </summary>
public static class TrialExtensionPolicy
{
    public const int MinExtensionDays = 1;
    public const int MaxExtensionDays = 30;
    public const int MaxRemainingDaysForExtension = 30;

    public const string DaysRequiredMessage = "Enter the number of days to extend the trial.";
    public const string DaysOutOfRangeMessage = "Days must be between 1 and 30.";
    public const string NoSubscriptionMessage = "Organization has no subscription.";
    public const string NotTrialingMessage =
        "Trial cannot be extended because the linked subscription is not in a trialing status.";
    public const string TooManyDaysRemainingMessage = "Trial cannot be extended because 30 or more days remain.";
    public const string ScheduleAttachedMessage =
        "Trial cannot be extended because the subscription has an active subscription schedule.";

    /// <summary>
    /// Whole days left in the trial, rounded up, measured against the subscription's test clock when one is attached.
    /// </summary>
    public static int GetRemainingDays(Subscription subscription)
    {
        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;
        return (int)Math.Ceiling((subscription.TrialEnd!.Value - now).TotalDays);
    }

    public static bool IsEligible(Subscription? subscription) =>
        subscription is { Status: StripeConstants.SubscriptionStatus.Trialing, TrialEnd: not null } &&
        string.IsNullOrEmpty(subscription.ScheduleId) &&
        GetRemainingDays(subscription) < MaxRemainingDaysForExtension;

    /// <summary>
    /// Returns a validation message when <paramref name="days"/> is not an acceptable extension, otherwise <see langword="null"/>.
    /// </summary>
    public static string? ValidateDays(int? days) => days switch
    {
        null => DaysRequiredMessage,
        < MinExtensionDays or > MaxExtensionDays => DaysOutOfRangeMessage,
        _ => null
    };
}
