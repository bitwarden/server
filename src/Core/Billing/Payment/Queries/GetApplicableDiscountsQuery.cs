#nullable enable

using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Models.Api.Response;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Payment.Queries;

public interface IGetApplicableDiscountsQuery
{
    /// <summary>
    /// Returns all discounts the user is eligible for, mapped to <see cref="SubscriptionDiscountResponseModel"/>.
    /// </summary>
    Task<BillingCommandResult<SubscriptionDiscountResponseModel[]>> Run(User user);
}

public class GetApplicableDiscountsQuery(
    ISubscriptionDiscountService subscriptionDiscountService) : IGetApplicableDiscountsQuery
{
    public async Task<BillingCommandResult<SubscriptionDiscountResponseModel[]>> Run(User user)
    {
        var eligibleDiscounts = await subscriptionDiscountService.GetEligibleDiscountsAsync(user);
        return eligibleDiscounts
            .Select(e => SubscriptionDiscountResponseModel.From(e.Discount, e.TierEligibility))
            .ToArray();
    }
}
