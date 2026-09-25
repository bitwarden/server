using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Subscriptions.Schedules.Enums;
using Stripe;

namespace Bit.Core.Billing.Subscriptions.Schedules;

using static StripeConstants;

/// <summary>
/// Classifies the Stripe subscription schedule attached to a subscription by the part of our code that
/// created it, read from the schedule's <see cref="MetadataKeys.ManagingSystem"/> metadata. Schedules created
/// before that marker existed are classified from the markers our code stamped onto their phases.
/// </summary>
public static class SubscriptionScheduleOwnershipMapper
{
    /// <summary>
    /// Classifies the schedule attached to <paramref name="subscription"/>, which must have been
    /// loaded with <c>schedule</c> expanded.
    /// </summary>
    public static SubscriptionScheduleOwnership Map(Subscription subscription)
    {
        if (string.IsNullOrEmpty(subscription.ScheduleId))
        {
            return SubscriptionScheduleOwnership.None;
        }

        var schedule = subscription.Schedule;
        return schedule is null
            ? SubscriptionScheduleOwnership.Unexpanded
            : MapSchedule(schedule);
    }

    /// <summary>
    /// Classifies a schedule that the caller already loaded. Use this when the schedule was fetched
    /// directly rather than expanded onto its subscription.
    /// </summary>
    public static SubscriptionScheduleOwnership MapSchedule(SubscriptionSchedule schedule)
    {
        if (schedule.Status != SubscriptionScheduleStatus.Active)
        {
            return SubscriptionScheduleOwnership.None;
        }

        if (schedule.Metadata != null &&
            schedule.Metadata.TryGetValue(MetadataKeys.ManagingSystem, out var managingSystem))
        {
            return managingSystem switch
            {
                ManagingSystems.AnnualUpgrade => SubscriptionScheduleOwnership.AnnualUpgrade,
                ManagingSystems.BusinessPriceIncrease => SubscriptionScheduleOwnership.BusinessPriceIncrease,
                ManagingSystems.PersonalPriceIncrease => SubscriptionScheduleOwnership.PersonalPriceIncrease,
                _ => SubscriptionScheduleOwnership.Foreign
            };
        }

        // Schedules created before the managing-system marker carry these markers on their phases.
        if (AnyPhaseCarries(schedule, MetadataKeys.AnnualUpgrade))
        {
            return SubscriptionScheduleOwnership.AnnualUpgrade;
        }

        return AnyPhaseCarries(schedule, MetadataKeys.MigrationCohortId)
            ? SubscriptionScheduleOwnership.BusinessPriceIncrease
            : SubscriptionScheduleOwnership.Foreign;
    }

    private static bool AnyPhaseCarries(SubscriptionSchedule schedule, string metadataKey) =>
        (schedule.Phases ?? []).Any(phase => phase.Metadata?.ContainsKey(metadataKey) == true);

    /// <summary>
    /// The metadata keys present across a schedule's phases. Values may carry customer detail, so
    /// only the keys are safe to log.
    /// </summary>
    public static string[] DistinctPhaseMetadataKeys(SubscriptionSchedule? schedule) =>
        [.. (schedule?.Phases ?? [])
            .SelectMany(phase => phase.Metadata?.Keys ?? Enumerable.Empty<string>())
            .Distinct()
            .Order()];
}
