#nullable enable

using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Models.Api.Response;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Payment.Queries;

public interface IGetApplicableDiscountsQuery
{
    /// <summary>
    /// Returns all discounts the user is eligible for, split into cart-level and item-level
    /// </summary>
    Task<BillingCommandResult<SubscriptionDiscountEligibilityResponseModel>> Run(User user);
}

public class GetApplicableDiscountsQuery(
    ISubscriptionDiscountService subscriptionDiscountService) : IGetApplicableDiscountsQuery
{
    public async Task<BillingCommandResult<SubscriptionDiscountEligibilityResponseModel>> Run(User user)
    {
        var eligibleDiscounts = await subscriptionDiscountService.GetEligibleDiscountsAsync(user);

        var cartLevel = new List<SubscriptionDiscountResponseModel>();
        var itemLevel = new List<SubscriptionDiscountResponseModel>();

        foreach (var eligibility in eligibleDiscounts)
        {
            var model = SubscriptionDiscountResponseModel.From(eligibility.Discount, eligibility.TierEligibility);
            if (eligibility.Discount.StripeProductIds is null or { Count: 0 })
            {
                cartLevel.Add(model);
            }
            else
            {
                itemLevel.Add(model);
            }
        }

        return new SubscriptionDiscountEligibilityResponseModel
        {
            CartLevelDiscounts = cartLevel.ToArray(),
            ItemLevelDiscounts = itemLevel.ToArray()
        };
    }
}
