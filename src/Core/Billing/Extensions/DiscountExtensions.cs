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
    /// Merges customer-level, existing subscription/phase, and newly applied coupon IDs into one
    /// ordered, de-duplicated list so the new coupon STACKS with pre-existing discounts. Stripe's
    /// subscription/phase-level discounts override the customer-level one, so the customer coupon
    /// must be copied into the array explicitly to stack. Order: customer first, then existing, then new.
    /// </summary>
    /// <param name="customerDiscount">Customer-level discount to carry over (any present coupon, regardless of validity), or null. Pass from an expanded customer object.</param>
    /// <param name="existingDiscountCouponIds">Coupon IDs already on the subscription/phase, in order (materialized — <c>d.Source.Coupon.Id</c> NPEs on unexpanded discounts).</param>
    /// <param name="newCouponIds">Coupon ID(s) being applied (churn / proactive / milestone).</param>
    /// <returns>Ordered, de-duplicated coupon IDs.</returns>
    public static IReadOnlyList<string> MergeDiscountCouponIds(
        this Discount? customerDiscount,
        IEnumerable<string?>? existingDiscountCouponIds,
        params string?[] newCouponIds)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string? couponId)
        {
            if (!string.IsNullOrEmpty(couponId) && seen.Add(couponId))
            {
                ordered.Add(couponId);
            }
        }

        // Customer coupon first; carried whenever present, regardless of validity.
        Add(customerDiscount?.Source?.Coupon?.Id);

        foreach (var id in existingDiscountCouponIds ?? [])
        {
            Add(id);
        }

        foreach (var id in newCouponIds)
        {
            Add(id);
        }

        return ordered;
    }

    public static List<SubscriptionDiscountOptions> ToSubscriptionDiscountOptions(
        this IReadOnlyList<string> couponIds) =>
        [.. couponIds.Select(id => new SubscriptionDiscountOptions { Coupon = id })];

    public static List<SubscriptionSchedulePhaseDiscountOptions> ToPhaseDiscountOptions(
        this IReadOnlyList<string> couponIds) =>
        [.. couponIds.Select(id => new SubscriptionSchedulePhaseDiscountOptions { Coupon = id })];

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
}
