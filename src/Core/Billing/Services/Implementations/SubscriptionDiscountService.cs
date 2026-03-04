using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Core.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Services.Implementations;

/// <inheritdoc />
public class SubscriptionDiscountService(
    ISubscriptionDiscountRepository subscriptionDiscountRepository,
    IDiscountAudienceFilterFactory discountAudienceFilterFactory,
    IStripeAdapter stripeAdapter,
    ILogger<SubscriptionDiscountService> logger) : ISubscriptionDiscountService
{
    /// <inheritdoc />
    public async Task<IEnumerable<DiscountEligibility>> GetEligibleDiscountsAsync(User user)
    {
        var activeDiscounts = await subscriptionDiscountRepository.GetActiveDiscountsAsync();
        var eligibleDiscounts = new List<DiscountEligibility>();

        foreach (var discount in activeDiscounts)
        {
            var tierEligibility = await GetTierEligibilityAsync(user, discount);
            // If tierEligibility is null, it means no filter is configured for the discount's audience type,
            // so we skip it since we can't determine eligibility. If it's not null, we check
            // if the user is eligible for at least one tier before adding it to the results.
            if (tierEligibility is not null && tierEligibility.Values.Any(isEligible => isEligible))
            {
                eligibleDiscounts.Add(new DiscountEligibility(discount, tierEligibility));
            }
        }

        return eligibleDiscounts;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateDiscountEligibilityForUserAsync(User user, string coupon, DiscountTierType tierType)
    {
        var discount = await subscriptionDiscountRepository.GetByStripeCouponIdAsync(coupon);
        if (discount == null  || !IsDiscountActive(discount))
        {
            return false;
        }

        // Validate Stripe-native coupon properties (validity)
        if (!await IsStripeCouponValidAsync(coupon))
        {
            logger.LogWarning("Deleting expired coupon {CouponId} from our table - discount is no longer active",
                discount.Id);
            await subscriptionDiscountRepository.DeleteAsync(discount);
            return false;
        }

        var tierEligibility = await GetTierEligibilityAsync(user, discount);
        return tierEligibility is not null && tierEligibility[tierType];
    }

    /// <summary>
    /// Returns the per-tier eligibility matrix for the given <paramref name="user"/> and <paramref name="discount"/>,
    /// or <see langword="null"/> if no filter is configured for the discount's audience type.
    /// </summary>
    private async Task<IDictionary<DiscountTierType, bool>?> GetTierEligibilityAsync(
        User user, SubscriptionDiscount discount)
    {
        var filter = discountAudienceFilterFactory.GetFilter(discount.AudienceType);
        return filter is not null ? await filter.IsUserEligible(user, discount) : null;
    }

    /// <summary>
    /// Checks if a discount is currently active based on its start and end dates.
    /// </summary>
    /// <param name="discount">The discount to check.</param>
    /// <returns><see langword="true"/> if the current time is within the discount's valid date range; otherwise, <see langword="false"/>.</returns>
    private static bool IsDiscountActive(SubscriptionDiscount discount)
    {
        var now = DateTime.UtcNow;
        return now >= discount.StartDate && now <= discount.EndDate;
    }

    /// <summary>
    /// Validates Stripe-native coupon properties including expiration date, redemption limits, and validity flag.
    /// </summary>
    /// <param name="couponId">The Stripe coupon ID to validate.</param>
    /// <returns><see langword="true"/> if the coupon is valid in Stripe; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> IsStripeCouponValidAsync(string couponId)
    {
        try
        {
            var stripeCoupon = await stripeAdapter.GetCouponAsync(couponId);

            // Check if coupon has been marked invalid in Stripe
            return stripeCoupon?.Valid == true;
        }
        catch
        {
            // If we can't fetch the coupon from Stripe, consider it invalid
            return false;
        }
    }
}
