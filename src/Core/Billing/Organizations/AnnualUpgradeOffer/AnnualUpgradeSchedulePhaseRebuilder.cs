using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.Schedules;
using Stripe;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer;

/// <summary>
/// Rebuilds the phases of an annual-upgrade schedule when the subscription is edited during the
/// pending window. Annual upgrade introduces no new coupon, so existing phase-level discounts reuse
/// the existing discount objects whereas the price-migration rewriter adds a coupon and must
/// merge the customer discount.
/// </summary>
public static class AnnualUpgradeSchedulePhaseRebuilder
{
    public static List<SubscriptionSchedulePhaseOptions> BuildUpdatedPhases(
        List<SubscriptionSchedulePhase> phases,
        IReadOnlyList<OrganizationSubscriptionChange> changes,
        Plan source,
        Plan target)
    {
        var result = new List<SubscriptionSchedulePhaseOptions>();

        // A lone remaining phase priced on the target plan is the annual phase; its changes must
        // translate against the target plan. Otherwise, the first phase is the active monthly term.
        var phase1UsesTargetPrices = phases.Count == 1 && SchedulePhaseMapper.PhaseUsesTargetPlanPrices(phases[0], target);

        result.Add(BuildPhaseOptions(phases[0], changes, source, phase1UsesTargetPrices ? target : source));

        if (phases.Count >= 2)
        {
            result.Add(BuildPhaseOptions(phases[1], changes, source, target));
        }

        return result;
    }

    private static SubscriptionSchedulePhaseOptions BuildPhaseOptions(
        SubscriptionSchedulePhase sourcePhase,
        IReadOnlyList<OrganizationSubscriptionChange> changes,
        Plan source,
        Plan target) =>
        new()
        {
            StartDate = sourcePhase.StartDate,
            EndDate = sourcePhase.EndDate,
            Items = SchedulePhaseMapper.ApplyChangesToPhaseItems(sourcePhase.Items, changes, source, target),
            Discounts = sourcePhase.Discounts is { Count: > 0 }
                ? [.. sourcePhase.Discounts.Select(PreservePhaseDiscount)]
                : null,
            Metadata = sourcePhase.Metadata,
            ProrationBehavior = sourcePhase.ProrationBehavior
        };

    /// Reuses the existing discount object so a temporary coupon is not re-minted (which would
    /// restart it at renewal). Fall back to coupon for any discountId not available for reuse.
    private static SubscriptionSchedulePhaseDiscountOptions PreservePhaseDiscount(
        SubscriptionSchedulePhaseDiscount discount) =>
        discount.DiscountId is { Length: > 0 }
            ? new SubscriptionSchedulePhaseDiscountOptions { Discount = discount.DiscountId }
            : new SubscriptionSchedulePhaseDiscountOptions { Coupon = discount.CouponId };
}
