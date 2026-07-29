using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Bit.Core.Billing.Organizations.Schedules.Models;
using Stripe;

namespace Bit.Core.Billing.Organizations.Schedules;

using static StripeConstants;

/// <summary>
/// Classifies the Stripe subscription schedule attached to an organization's subscription, from the
/// metadata Bitwarden stamps onto the phases of every schedule it creates. A schedule carrying
/// neither marker was built outside this codebase, for example by hand in the Stripe Dashboard to
/// hold a negotiated renewal, and operations that would release or rewrite a schedule must leave it
/// alone.
/// </summary>
/// <remarks>
/// Pure. No Stripe calls, no database reads, no dependencies. Reading the schedule rather than the
/// organization is the point: ownership is written into the schedule at creation time, not derived
/// after the fact from the organization's cohort assignment, which redemption deletes and which is
/// never cleared once a migration completes.
/// </remarks>
public static class SubscriptionScheduleOwnershipMapper
{
    /// <summary>
    /// Classifies the schedule attached to <paramref name="subscription"/>, which must have been
    /// loaded with <c>schedule</c> expanded.
    /// </summary>
    /// <returns>
    /// Null when the subscription reports a schedule ID but the caller did not expand it. Null is
    /// deliberately not <see cref="OrganizationSubscriptionScheduleOwnership.None"/>: None means
    /// nothing is attached, and a caller told that would release nothing and then create a second
    /// schedule, which Stripe rejects because it permits one active schedule per subscription.
    /// Callers choose the posture, so a page load can suppress quietly while a mutation refuses.
    /// </returns>
    public static OrganizationSubscriptionScheduleOwnershipResult? MapOrNull(Subscription subscription) =>
        string.IsNullOrEmpty(subscription.ScheduleId)
            ? Map(null)
            : subscription.Schedule is null
                ? null
                : Map(subscription.Schedule);

    /// <summary>
    /// Classifies a schedule the caller already holds, for example one read from a schedule listing
    /// rather than from an expanded subscription.
    /// </summary>
    public static OrganizationSubscriptionScheduleOwnershipResult Map(SubscriptionSchedule? schedule)
    {
        if (schedule is null || schedule.Status != SubscriptionScheduleStatus.Active)
        {
            return new OrganizationSubscriptionScheduleOwnershipResult(
                OrganizationSubscriptionScheduleOwnership.None, null);
        }

        if (AnyPhaseCarries(schedule, MetadataKeys.AnnualUpgrade))
        {
            return new OrganizationSubscriptionScheduleOwnershipResult(
                OrganizationSubscriptionScheduleOwnership.AnnualUpgrade, schedule);
        }

        if (AnyPhaseCarries(schedule, MetadataKeys.MigrationCohortId))
        {
            return new OrganizationSubscriptionScheduleOwnershipResult(
                OrganizationSubscriptionScheduleOwnership.PriceMigration, schedule);
        }

        return new OrganizationSubscriptionScheduleOwnershipResult(
            OrganizationSubscriptionScheduleOwnership.Foreign, schedule);
    }

    private static bool AnyPhaseCarries(SubscriptionSchedule schedule, string metadataKey) =>
        (schedule.Phases ?? []).Any(phase => phase.Metadata?.ContainsKey(metadataKey) == true);

    /// <summary>
    /// The metadata keys present across a schedule's phases, keys only. Values may carry customer
    /// detail, so only the keys are safe to log, and the keys are what distinguish a hand-built
    /// schedule from one of ours that lost its marker.
    /// </summary>
    public static string[] DistinctPhaseMetadataKeys(SubscriptionSchedule? schedule) =>
        [.. (schedule?.Phases ?? [])
            .SelectMany(phase => phase.Metadata?.Keys ?? Enumerable.Empty<string>())
            .Distinct()
            .Order()];
}
