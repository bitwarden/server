using Bit.Core.Billing.Enums;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services;

/// <summary>
/// Manages eligibility evaluation for subscription discounts.
/// </summary>
public interface ISubscriptionDiscountService
{
    /// <summary>
    /// Retrieves all active discounts the user is eligible for.
    /// </summary>
    /// <param name="user">The user to evaluate discount eligibility for.</param>
    /// <returns>The collection of <see cref="DiscountEligibility"/> records pairing each eligible discount with its tier eligibility matrix.</returns>
    Task<IEnumerable<DiscountEligibility>> GetEligibleDiscountsAsync(User user);

    /// <summary>
    /// Performs a server-side eligibility recheck for a specific coupon before subscription creation,
    /// confirming the coupon exists, is active, and the user still qualifies for it on the specified tier.
    /// </summary>
    /// <param name="user">The user to validate eligibility for.</param>
    /// <param name="coupon">The Stripe coupon ID to validate.</param>
    /// <param name="tierType">The product tier the user intends to subscribe to.</param>
    /// <returns><see langword="true"/> if the discount exists and the user is eligible for the given tier; otherwise <see langword="false"/>.</returns>
    Task<bool> ValidateDiscountEligibilityForUserAsync(User user, string coupon, DiscountTierType tierType);
}
