using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models;
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
    public async Task<bool> ValidateDiscountEligibilityForUserAsync(User user, IReadOnlyList<string> couponIds, DiscountTierType tierType)
    {
        var eligibleDiscounts = await GetEligibleDiscountsAsync(user);
        var eligibilityByStripeCouponId = eligibleDiscounts.ToDictionary(d => d.Discount.StripeCouponId);
        return couponIds.All(id =>
            eligibilityByStripeCouponId.TryGetValue(id, out var eligibility) &&
            eligibility.TierEligibility[tierType]);
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
}
