using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Stripe;

namespace Bit.Core.Billing.Organizations.Schedules;

using static StripeConstants;

/// <summary>
/// Classifies the Stripe subscription schedule attached to an organization's subscription, from the
/// metadata our code stamps onto the phases of every organization schedule it creates.
/// </summary>
public static class SubscriptionScheduleOwnershipMapper
{
    /// <summary>
    /// Classifies the schedule attached to <paramref name="subscription"/>, which must have been
    /// loaded with <c>schedule</c> expanded.
    /// </summary>
    public static OrganizationSubscriptionScheduleOwnership Map(Subscription subscription)
    {
        if (string.IsNullOrEmpty(subscription.ScheduleId))
        {
            return OrganizationSubscriptionScheduleOwnership.None;
        }

        var schedule = subscription.Schedule;
        return schedule is null
            ? OrganizationSubscriptionScheduleOwnership.Unexpanded
            : MapSchedule(schedule);
    }

    /// <summary>
    /// Classifies a schedule that the caller already loaded. Use this when the schedule was fetched
    /// directly rather than expanded onto its subscription.
    /// </summary>
    public static OrganizationSubscriptionScheduleOwnership MapSchedule(SubscriptionSchedule schedule)
    {
        if (schedule.Status != SubscriptionScheduleStatus.Active)
        {
            return OrganizationSubscriptionScheduleOwnership.None;
        }

        if (AnyPhaseCarries(schedule, MetadataKeys.AnnualUpgrade))
        {
            return OrganizationSubscriptionScheduleOwnership.AnnualUpgrade;
        }

        return AnyPhaseCarries(schedule, MetadataKeys.MigrationCohortId)
            ? OrganizationSubscriptionScheduleOwnership.PriceMigration
            : OrganizationSubscriptionScheduleOwnership.Foreign;
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
