using Bit.Core.Entities;

namespace Bit.Core.Billing.Services;

public interface ISubscriptionDiscountService
{
    /// <summary>
    /// Validates whether a user is eligible for a specific discount coupon during subscription creation.
    /// </summary>
    /// <param name="user">The user attempting to use the discount.</param>
    /// <param name="stripeCouponId">The Stripe coupon ID to validate.</param>
    /// <returns>
    /// <see langword="true"/> if the discount exists, is currently active, and the user meets eligibility criteria;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This method performs server-side validation to ensure:
    /// <list type="bullet">
    /// <item>The discount exists in the database</item>
    /// <item>The discount is within its valid date range</item>
    /// <item>The user meets the audience targeting criteria for the discount</item>
    /// </list>
    /// </remarks>
    Task<bool> ValidateDiscountForUserAsync(User user, string stripeCouponId);
}
