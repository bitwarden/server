using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services.Implementations;

/// <inheritdoc />
public class SubscriptionDiscountService(
    ISubscriptionDiscountRepository subscriptionDiscountRepository,
    IDiscountAudienceFilterFactory discountAudienceFilterFactory) : ISubscriptionDiscountService
{
    /// <inheritdoc />
    public async Task<IEnumerable<SubscriptionDiscount>> GetEligibleDiscountsAsync(User user)
    {
        var activeDiscounts = await subscriptionDiscountRepository.GetActiveDiscountsAsync();
        return activeDiscounts.Where(discount => IsEligible(user, discount)).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> ValidateDiscountEligibilityForUserAsync(User user, string coupon)
    {
        var discount = await subscriptionDiscountRepository.GetByStripeCouponIdAsync(coupon);
        return discount != null && IsEligible(user, discount);
    }

    /// <summary>
    /// Checks whether the <paramref name="user"/> meets the audience criteria for the given <paramref name="discount"/>
    /// by delegating to the appropriate <see cref="IDiscountAudienceFilter"/> via the factory.
    /// </summary>
    private bool IsEligible(User user, SubscriptionDiscount discount)
    {
        if (discount.AudienceType == DiscountAudienceType.AllUsers)
        {
            return true;
        }

        var filter = discountAudienceFilterFactory.GetFilter(discount.AudienceType);
        return filter?.IsUserEligible(user, discount) ?? false;
    }
}
