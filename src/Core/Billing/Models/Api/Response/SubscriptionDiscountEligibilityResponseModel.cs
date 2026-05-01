#nullable enable

namespace Bit.Core.Billing.Models.Api.Response;

public class SubscriptionDiscountEligibilityResponseModel
{
    public SubscriptionDiscountResponseModel[] CartLevelDiscounts { get; init; } = [];
    public SubscriptionDiscountResponseModel[] ItemLevelDiscounts { get; init; } = [];
}
