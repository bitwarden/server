using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services;

/// <summary>
/// Manages eligibility evaluation for subscription discounts.
/// </summary>
public interface ISubscriptionDiscountService
{
    /// <summary>
    /// Retrieves all active discounts the user is eligible for
    /// </summary>
    /// <param name="user">The user to evaluate discount eligibility for.</param>
    /// <returns>The collection of eligible <see cref="SubscriptionDiscount"/> records.</returns>
    Task<IEnumerable<SubscriptionDiscount>> GetEligibleDiscountsAsync(User user);

    /// <summary>
    /// Performs a server-side eligibility recheck for a specific coupon before subscription creation,
    /// confirming the coupon exists, is active, and the user still qualifies for it.
    /// </summary>
    /// <param name="user">The user to validate eligibility for.</param>
    /// <param name="coupon">The Stripe coupon ID to validate.</param>
    /// <returns><see langword="true"/> if the discount exists and the user is eligible; otherwise <see langword="false"/>.</returns>
    Task<bool> ValidateDiscountEligibilityForUserAsync(User user, string coupon);
}
