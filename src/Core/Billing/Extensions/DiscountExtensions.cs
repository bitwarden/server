using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class DiscountExtensions
{
    public static bool AppliesTo(this Discount discount, SubscriptionItem subscriptionItem)
        => discount.Coupon.AppliesTo.Products.Contains(subscriptionItem.Price.Product.Id);

    public static bool AppliesTo(this Coupon coupon, SubscriptionItem subscriptionItem)
        => coupon.AppliesTo?.Products?.Contains(subscriptionItem.Price.Product.Id) ?? false;

    public static bool IsValid(this Discount? discount)
        => discount?.Coupon?.Valid ?? false;

    /// <summary>
    /// Merges customer-level, existing subscription/phase, and newly applied coupon IDs into one
    /// ordered, de-duplicated list so the new coupon STACKS with pre-existing discounts. Stripe's
    /// subscription/phase-level discounts override the customer-level one, so the customer coupon
    /// must be copied into the array explicitly to stack. Order: customer first, then existing, then new.
    /// </summary>
    /// <param name="customerDiscount">Customer-level discount to carry over (any present coupon, regardless of validity), or null. Pass from an expanded customer object.</param>
    /// <param name="existingDiscountCouponIds">Coupon IDs already on the subscription/phase, in order (materialized — <c>d.Coupon.Id</c> NPEs on unexpanded discounts).</param>
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
        Add(customerDiscount?.Coupon?.Id);

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
}
