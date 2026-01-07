using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class DiscountExtensions
{
    public static bool AppliesTo(this Discount discount, SubscriptionItem subscriptionItem)
        => discount.Coupon.AppliesTo.Products.Contains(subscriptionItem.Price.Product.Id);

    public static bool IsValid(this Discount? discount)
        => discount?.Coupon?.Valid ?? false;
}
