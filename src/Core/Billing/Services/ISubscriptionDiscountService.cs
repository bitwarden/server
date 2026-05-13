using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
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
    /// Performs a server-side eligibility recheck for the provided coupon IDs before subscription creation,
    /// confirming every coupon exists, is active, and the user qualifies for each on the specified tier.
    /// </summary>
    /// <param name="user">The user to validate eligibility for.</param>
    /// <param name="couponIds">The Stripe coupon IDs to validate.</param>
    /// <param name="tierType">The product tier the user intends to subscribe to.</param>
    /// <returns><see langword="true"/> if all coupons are found in the user's eligible discounts and tier eligibility is <see langword="true"/> for <paramref name="tierType"/>; otherwise <see langword="false"/>.</returns>
    Task<bool> ValidateDiscountEligibilityForUserAsync(User user, IReadOnlyList<string> couponIds, DiscountTierType tierType);
}
