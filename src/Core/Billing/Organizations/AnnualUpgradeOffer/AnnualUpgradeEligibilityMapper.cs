using Bit.Core.Billing.Organizations.PlanMigration;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Stripe;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer;

internal enum AnnualUpgradeIneligibleReason
{
    UnusableDiscounts,
    UnexpandedSchedule,
    AlreadyScheduled,
    ForeignSchedule,
    UnmappableLine
}

/// <summary>A subscription line item paired with the annual price that replaces it.</summary>
internal readonly record struct AnnualUpgradeLine(SubscriptionItem Item, string TargetPriceId);

internal readonly record struct AnnualUpgradeEligibility(
    AnnualUpgradeIneligibleReason? Reason,
    IReadOnlyList<AnnualUpgradeLine> Lines,
    string? UnmappablePriceId)
{
    public bool IsEligible => Reason is null;
}

/// <summary>
/// Maps a subscription and its plan pair to the annual lines a switch would build, or the reason it
/// cannot. Callers own the logging: the same reason is routine on a page load and a refusal at
/// redemption.
/// </summary>
internal static class AnnualUpgradeEligibilityMapper
{
    public static AnnualUpgradeEligibility Map(
        Subscription subscription, Plan currentPlan, Plan annualLatestPlan)
    {
        if (HasUnusableDiscounts(subscription))
        {
            return Ineligible(AnnualUpgradeIneligibleReason.UnusableDiscounts);
        }

        // Discard-free so CS8509 catches an unhandled ownership added later.
        var scheduleReason = SubscriptionScheduleOwnershipMapper.Map(subscription) switch
        {
            OrganizationSubscriptionScheduleOwnership.Unexpanded => AnnualUpgradeIneligibleReason.UnexpandedSchedule,
            OrganizationSubscriptionScheduleOwnership.AnnualUpgrade => AnnualUpgradeIneligibleReason.AlreadyScheduled,
            OrganizationSubscriptionScheduleOwnership.Foreign => AnnualUpgradeIneligibleReason.ForeignSchedule,
            OrganizationSubscriptionScheduleOwnership.None or
                OrganizationSubscriptionScheduleOwnership.PriceMigration => (AnnualUpgradeIneligibleReason?)null
        };
        if (scheduleReason is not null)
        {
            return Ineligible(scheduleReason.Value);
        }

        var lines = new List<AnnualUpgradeLine>();
        foreach (var item in subscription.Items.Data)
        {
            // No price object to map; fail the subscription rather than drop the line.
            if (item.Price?.Id is null)
            {
                return Ineligible(AnnualUpgradeIneligibleReason.UnmappableLine);
            }

            var targetPriceId = OrganizationPlanMigrationPriceMapper.MapOrNull(
                item.Price.Id, currentPlan, annualLatestPlan);
            if (targetPriceId is null)
            {
                return Ineligible(AnnualUpgradeIneligibleReason.UnmappableLine, item.Price.Id);
            }

            lines.Add(new AnnualUpgradeLine(item, targetPriceId));
        }

        return lines.Count == 0
            ? Ineligible(AnnualUpgradeIneligibleReason.UnmappableLine)
            : new AnnualUpgradeEligibility(null, lines, null);
    }

    private static AnnualUpgradeEligibility Ineligible(
        AnnualUpgradeIneligibleReason reason, string? unmappablePriceId = null) =>
        new(reason, [], unmappablePriceId);

    private static bool IsUnusable(Discount? discount) =>
        discount is null || string.IsNullOrEmpty(discount.Coupon?.Id);

    // Dropped by the Phase 2 filter, which would let the customer-level coupon resurrect instead of failing loudly.
    private static bool HasUnusableDiscounts(Subscription subscription) =>
        (subscription.Discounts ?? []).Any(IsUnusable) ||
        subscription.Items.Data.Any(item => (item.Discounts ?? []).Any(IsUnusable));
}
