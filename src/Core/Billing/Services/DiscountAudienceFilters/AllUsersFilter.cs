using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services.DiscountAudienceFilters;

public class AllUsersFilter : IDiscountAudienceFilter
{
    public DiscountAudienceType SupportedType => DiscountAudienceType.AllUsers;

    public Task<IDictionary<DiscountTierType, bool>> IsUserEligible(User user, SubscriptionDiscount discount)
    {
        var eligibleTiers = Utilities.GetTierEligibilityDictionary();

        if (discount.StripeProductIds == null || !discount.StripeProductIds.Any())
        {
            // If no product IDs are specified, the discount applies to all tiers
            foreach (var tier in eligibleTiers.Keys.ToList())
            {
                eligibleTiers[tier] = true;
            }
            return Task.FromResult(eligibleTiers);
        }

        foreach (var tier in discount.StripeProductIds)
        {
            var discountTier = StripeConstants.ProductIDs.GetProductTier(tier);
            if (discountTier != null)
            {
                eligibleTiers[discountTier.Value] = true;
            }
        }

        return Task.FromResult(eligibleTiers);
    }
}
