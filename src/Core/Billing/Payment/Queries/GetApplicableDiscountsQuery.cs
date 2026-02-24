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
        var discounts = await subscriptionDiscountService.GetEligibleDiscountsAsync(user);
        return discounts.Select(SubscriptionDiscountResponseModel.From).ToArray();
    }
}