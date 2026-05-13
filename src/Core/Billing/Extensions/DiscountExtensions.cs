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
}
