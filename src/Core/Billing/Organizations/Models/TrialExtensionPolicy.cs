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
    /// Whole days left in the trial, rounded up, measured against the test clock when attached, otherwise UTC now; requires <see cref="Subscription.TrialEnd"/>.
    /// </summary>
    public static int GetRemainingDays(Subscription subscription)
    {
        var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;
        return (int)Math.Ceiling((subscription.TrialEnd!.Value - now).TotalDays);
    }

    /// <summary>
    /// Returns the user-facing reason the trial cannot be extended, or <see langword="null"/> when it can.
    /// This is the single definition of eligibility; the Admin Edit page and the extend command both rely on it.
    /// </summary>
    public static string? GetIneligibilityReason(Subscription? subscription) => subscription switch
    {
        null => NoSubscriptionMessage,
        not { Status: StripeConstants.SubscriptionStatus.Trialing, TrialEnd: not null } => NotTrialingMessage,
        _ when GetRemainingDays(subscription) >= MaxRemainingDaysForExtension => TooManyDaysRemainingMessage,
        // Stripe discourages direct subscription updates while a schedule is attached; the schedule would own the trial end.
        { ScheduleId: { Length: > 0 } } => ScheduleAttachedMessage,
        _ => null
    };

    public static bool IsEligible(Subscription? subscription) => GetIneligibilityReason(subscription) is null;

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
