using Bit.Core.Billing.Organizations.AnnualUpgradeOffer.Models;
using Bit.Core.Billing.Organizations.PlanMigration;
using Bit.Core.Billing.Organizations.Schedules;
using Bit.Core.Billing.Organizations.Schedules.Enums;
using Microsoft.Extensions.Logging;
using Stripe;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Billing.Organizations.AnnualUpgradeOffer;

/// <summary>
/// Maps a subscription and its plan pair to the annual lines a monthly-to-annual switch would build, or null when
/// they cannot be built. Logs the reason against the calling class's logger.
/// </summary>
internal static class AnnualUpgradeLineMapper
{
    public static IReadOnlyList<AnnualUpgradeLine>? MapOrNull<T>(
        ILogger<T> logger,
        Guid organizationId,
        Subscription subscription,
        Plan currentPlan,
        Plan annualLatestPlan)
    {
        var caller = typeof(T).Name;

        if (HasUnusableDiscounts(subscription))
        {
            logger.LogError(
                "{Caller}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) has an unexpanded or couponless discount; refusing the annual upgrade",
                caller, subscription.Id, organizationId);
            return null;
        }

        switch (SubscriptionScheduleOwnershipMapper.Map(subscription))
        {
            case OrganizationSubscriptionScheduleOwnership.Unexpanded:
                logger.LogError(
                    "{Caller}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) reports schedule ({ScheduleId}) but it was not expanded; refusing the annual upgrade",
                    caller, subscription.Id, organizationId, subscription.ScheduleId);
                return null;

            case OrganizationSubscriptionScheduleOwnership.AnnualUpgrade:
                logger.LogInformation(
                    "{Caller}: Organization ({OrganizationId}) already redeemed the annual upgrade offer",
                    caller, organizationId);
                return null;

            case OrganizationSubscriptionScheduleOwnership.Foreign:
                logger.LogWarning(
                    "{Caller}: Organization ({OrganizationId}) has an unrecognized schedule ({ScheduleId}) on subscription ({SubscriptionId}); phase metadata keys present: {MetadataKeys}; refusing the annual upgrade",
                    caller, organizationId, subscription.ScheduleId, subscription.Id,
                    string.Join(", ", SubscriptionScheduleOwnershipMapper.DistinctPhaseMetadataKeys(subscription.Schedule)));
                return null;

            case OrganizationSubscriptionScheduleOwnership.None:
            case OrganizationSubscriptionScheduleOwnership.PriceMigration:
                break;

            default:
                logger.LogError(
                    "{Caller}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) has an unhandled schedule ownership; refusing the annual upgrade",
                    caller, subscription.Id, organizationId);
                return null;
        }

        var lines = new List<AnnualUpgradeLine>();
        foreach (var item in subscription.Items.Data)
        {
            var sourcePriceId = item.Price?.Id;
            var targetPriceId = sourcePriceId is null
                ? null
                : OrganizationPlanMigrationPriceMapper.MapOrNull(sourcePriceId, currentPlan, annualLatestPlan);

            // One unmappable line refuses the whole subscription.
            if (targetPriceId is null)
            {
                logger.LogWarning(
                    "{Caller}: Subscription ({SubscriptionId}) line item price ({PriceId}) has no annual-latest mapping for Organization ({OrganizationId}); refusing the annual upgrade",
                    caller, subscription.Id, sourcePriceId, organizationId);
                return null;
            }

            lines.Add(new AnnualUpgradeLine(item, targetPriceId));
        }

        if (lines.Count == 0)
        {
            logger.LogWarning(
                "{Caller}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) has no line items to map; refusing the annual upgrade",
                caller, subscription.Id, organizationId);
            return null;
        }

        return lines;
    }

    private static bool IsUnusable(Discount? discount) =>
        discount is null || string.IsNullOrEmpty(discount.Source?.CouponId);

    // A discount with no coupon id would only come from a promotion-code source; Bitwarden applies discounts
    // exclusively via bare coupons (no promotion codes), so every real discount has one and this never rejects
    // a valid subscriber. It exists to refuse rather than silently drop a discount that couldn't be carried.
    // If promotion-code discounts are ever introduced, revisit this: a promo code's coupon lives on
    // discount.Coupon, not necessarily Source.CouponId.
    private static bool HasUnusableDiscounts(Subscription subscription) =>
        (subscription.Discounts ?? []).Any(IsUnusable) ||
        subscription.Items.Data.Any(item => (item.Discounts ?? []).Any(IsUnusable));
}
