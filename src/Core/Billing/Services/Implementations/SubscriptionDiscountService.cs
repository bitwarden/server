#nullable enable

using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services.Implementations;

public class SubscriptionDiscountService(
    ISubscriptionDiscountRepository subscriptionDiscountRepository) : ISubscriptionDiscountService
{
    public async Task<bool> ValidateDiscountForUserAsync(User user, string stripeCouponId)
    {
        var discount = await subscriptionDiscountRepository.GetByStripeCouponIdAsync(stripeCouponId);

        if (discount == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (now < discount.StartDate || now > discount.EndDate)
        {
            return false;
        }

        return discount.AudienceType switch
        {
            DiscountAudienceType.UserHasNoPreviousSubscriptions =>
                !user.Premium && string.IsNullOrEmpty(user.GatewaySubscriptionId),
            _ => false
        };
    }
}
