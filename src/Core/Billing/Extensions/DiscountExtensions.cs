using Microsoft.Extensions.Logging;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Billing.Extensions;

public static class DiscountExtensions
{
    public static bool AppliesTo(this Coupon coupon, SubscriptionItem subscriptionItem)
        => coupon.AppliesTo?.Products?.Contains(subscriptionItem.Price.Product.Id) ?? false;

    public static bool IsValid(this Discount? discount)
        => discount?.Source?.Coupon?.Valid ?? false;

    public static bool IsForever(this Coupon? coupon) =>
        coupon is not null &&
        string.Equals(coupon.Duration, CouponDurations.Forever, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds phase-level discounts, de-duplicated by coupon: the customer's coupon (by coupon id),
    /// the subscription's live discounts (by discount id, so a one-time coupon isn't re-granted),
    /// coupons preserved from a future phase (by coupon id — they live on the phase, not the
    /// subscription), then new coupons (by coupon id). Returns null when empty; an empty array would
    /// delete the phase's discounts.
    /// </summary>
    /// <param name="subscription">The live subscription whose customer and subscription discounts are carried forward.</param>
    /// <param name="newCouponIds">Coupon IDs being newly applied to the phase.</param>
    /// <param name="preservedCouponIds">Coupon IDs preserved from a future phase that has no equivalent on the live subscription.</param>
    public static List<SubscriptionSchedulePhaseDiscountOptions>? BuildPhaseLevelDiscounts(
        Subscription subscription,
        IReadOnlyList<string> newCouponIds,
        IEnumerable<string?>? preservedCouponIds = null)
    {
        var discounts = new List<SubscriptionSchedulePhaseDiscountOptions>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var customerCouponId = subscription.Customer?.Discount?.Source?.CouponId;
        if (!string.IsNullOrEmpty(customerCouponId) && seen.Add(customerCouponId))
        {
            discounts.Add(new SubscriptionSchedulePhaseDiscountOptions { Coupon = customerCouponId });
        }

        foreach (var discount in subscription.Discounts ?? [])
        {
            var couponId = discount.Source?.CouponId;
            if (couponId is not null && !seen.Add(couponId))
            {
                continue;
            }
            discounts.Add(new SubscriptionSchedulePhaseDiscountOptions { Discount = discount.Id });
        }

        foreach (var couponId in preservedCouponIds ?? [])
        {
            if (!string.IsNullOrEmpty(couponId) && seen.Add(couponId))
            {
                discounts.Add(new SubscriptionSchedulePhaseDiscountOptions { Coupon = couponId });
            }
        }

        foreach (var couponId in newCouponIds)
        {
            if (!string.IsNullOrEmpty(couponId) && seen.Add(couponId))
            {
                discounts.Add(new SubscriptionSchedulePhaseDiscountOptions { Coupon = couponId });
            }
        }

        return discounts.Count == 0 ? null : discounts;
    }

    /// <summary>
    /// Discounts for a phase that is already active (its start date is in the past): carries only the
    /// discounts still live on the subscription, by discount id. Unlike <see cref="BuildPhaseLevelDiscounts"/>,
    /// the customer's coupon is deliberately omitted. When a phase carries explicit discounts Stripe
    /// suppresses the customer-level coupon, so listing it would newly stack it onto the current period
    /// (an over-charge in the customer's favor); when the phase carries none, the customer coupon still
    /// cascades on its own. Either way the active phase's effective discount is unchanged. Returns null
    /// when empty; an empty array would delete the phase's discounts.
    /// </summary>
    /// <param name="subscription">The live subscription whose active-phase discounts are carried forward.</param>
    public static List<SubscriptionSchedulePhaseDiscountOptions>? BuildCurrentPhaseDiscounts(Subscription subscription) =>
        subscription.Discounts is { Count: > 0 }
            ? subscription.Discounts
                .Select(discount => new SubscriptionSchedulePhaseDiscountOptions { Discount = discount.Id })
                .ToList()
            : null;

    /// <summary>
    /// Subscription-scope equivalent of <see cref="BuildPhaseLevelDiscounts"/>: live discounts by
    /// discount id, customer and new coupons by coupon id. Returns null when empty.
    /// </summary>
    /// <param name="subscription">The live subscription whose customer and subscription discounts are carried forward.</param>
    /// <param name="newCouponIds">Coupon IDs being newly applied to the subscription.</param>
    public static List<SubscriptionDiscountOptions>? BuildSubscriptionLevelDiscounts(
        Subscription subscription,
        IReadOnlyList<string> newCouponIds)
    {
        var discounts = new List<SubscriptionDiscountOptions>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var customerCouponId = subscription.Customer?.Discount?.Source?.CouponId;
        if (!string.IsNullOrEmpty(customerCouponId) && seen.Add(customerCouponId))
        {
            discounts.Add(new SubscriptionDiscountOptions { Coupon = customerCouponId });
        }

        foreach (var discount in subscription.Discounts ?? [])
        {
            var couponId = discount.Source?.CouponId;
            if (couponId is not null && !seen.Add(couponId))
            {
                continue;
            }
            discounts.Add(new SubscriptionDiscountOptions { Discount = discount.Id });
        }

        foreach (var couponId in newCouponIds)
        {
            if (!string.IsNullOrEmpty(couponId) && seen.Add(couponId))
            {
                discounts.Add(new SubscriptionDiscountOptions { Coupon = couponId });
            }
        }

        return discounts.Count == 0 ? null : discounts;
    }

    /// <summary>
    /// Builds item-level discounts from coupon ids only — Stripe rejects a discount id on a phase item.
    /// De-duplicates, skips empty ids, returns null when empty.
    /// </summary>
    /// <param name="couponIds">Coupon IDs to apply to the phase item.</param>
    public static List<SubscriptionSchedulePhaseItemDiscountOptions>? BuildPhaseItemLevelDiscounts(
        IEnumerable<string?> couponIds)
    {
        var discounts = new List<SubscriptionSchedulePhaseItemDiscountOptions>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var couponId in couponIds)
        {
            if (!string.IsNullOrEmpty(couponId) && seen.Add(couponId))
            {
                discounts.Add(new SubscriptionSchedulePhaseItemDiscountOptions { Coupon = couponId });
            }
        }

        return discounts.Count == 0 ? null : discounts;
    }

    /// <summary>
    /// Throws when <paramref name="subscription"/> is missing an expansion the discount builders rely on:
    /// <c>discounts</c> present only as unexpanded id stubs, a missing <c>customer</c> (either would silently
    /// drop discounts), or a <c>test_clock</c> left unexpanded when the subscription is on one (its absence
    /// resolves the current phase against the wrong time, which flips the current-vs-future decision that
    /// drives discount carry-over). Logs before throwing.
    /// </summary>
    /// <param name="subscription">The subscription to check for missing expansions.</param>
    /// <param name="logger">Logger used to record the failure before throwing.</param>
    public static void RequireScheduleDiscountExpansions(Subscription subscription, ILogger logger)
    {
        if (subscription.Discounts is { Count: > 0 } && subscription.Discounts.Any(discount => discount is null))
        {
            logger.LogError(
                "Subscription {SubscriptionId} was loaded without expanding \"discounts\"; existing discounts would be silently dropped",
                subscription.Id);
            throw new InvalidOperationException(
                $"Subscription {subscription.Id} was loaded without expanding \"discounts\". Expand \"discounts.source.coupon\" first.");
        }

        if (subscription.Customer is null)
        {
            logger.LogError(
                "Subscription {SubscriptionId} was loaded without expanding \"customer\"; a customer-level coupon would be silently dropped",
                subscription.Id);
            throw new InvalidOperationException(
                $"Subscription {subscription.Id} was loaded without expanding \"customer\". Expand \"customer.discount.source.coupon\" first.");
        }

        if (subscription.TestClockId is not null && subscription.TestClock is null)
        {
            logger.LogError(
                "Subscription {SubscriptionId} is on test clock {TestClockId}, which was not expanded",
                subscription.Id, subscription.TestClockId);
            throw new InvalidOperationException(
                $"Subscription {subscription.Id} is on a test clock that was not expanded. Expand \"test_clock\" first.");
        }
    }
}
