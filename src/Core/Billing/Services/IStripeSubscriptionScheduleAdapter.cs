using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Billing.Services;

/// <summary>
/// Stripe subscription schedule operations. <see cref="CreateSubscriptionScheduleWithPhasesAsync"/> is the only
/// way our code creates a schedule, and it marks every schedule with the part of our code that owns it so it can
/// be told apart from schedules created by hand in the Stripe Dashboard.
/// </summary>
public interface IStripeSubscriptionScheduleAdapter
{
    /// <summary>
    /// Creates a two-phase schedule from <paramref name="subscription"/>: phase 1 mirrors the subscription as
    /// Stripe copied it, phase 2 is <paramref name="phase2Options"/>, and the schedule releases after phase 2.
    /// Stripe rejects metadata, phases, and end behavior on a create that uses <c>from_subscription</c>, so they
    /// are applied by the update that follows. Releases the schedule and rethrows if that update fails.
    /// </summary>
    /// <param name="subscription">The subscription, loaded with <c>discounts</c> expanded.</param>
    /// <param name="phase2Options">The phase that follows the subscription's current period.</param>
    /// <param name="managingSystem">One of <see cref="ManagingSystems"/>.</param>
    /// <param name="phaseMetadata">Metadata for both phases, or null for none.</param>
    Task<SubscriptionSchedule> CreateSubscriptionScheduleWithPhasesAsync(
        Subscription subscription,
        SubscriptionSchedulePhaseOptions phase2Options,
        string managingSystem,
        Dictionary<string, string>? phaseMetadata = null);

    Task<SubscriptionSchedule> GetSubscriptionScheduleAsync(string id, SubscriptionScheduleGetOptions? options = null);
    Task<StripeList<SubscriptionSchedule>> ListSubscriptionSchedulesAsync(SubscriptionScheduleListOptions options);
    Task<SubscriptionSchedule> UpdateSubscriptionScheduleAsync(string id, SubscriptionScheduleUpdateOptions options);
    Task<SubscriptionSchedule> ReleaseSubscriptionScheduleAsync(string id, SubscriptionScheduleReleaseOptions? options = null);
}
