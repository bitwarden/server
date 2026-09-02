using Bit.Core.Billing.Subscriptions.Repositories;
using Stripe;
using Event = Stripe.Event;

namespace Bit.Billing.Services.Implementations;

public class CouponDeletedHandler(
    ILogger<CouponDeletedHandler> logger,
    ISubscriptionDiscountRepository subscriptionDiscountRepository) : ICouponDeletedHandler
{
    public async Task HandleAsync(Event parsedEvent)
    {
        if (parsedEvent.Data.Object is not Coupon coupon)
        {
            logger.LogWarning("Received coupon.deleted event with unexpected object type. Event ID: {EventId}", parsedEvent.Id);
            return;
        }

        var discount = await subscriptionDiscountRepository.GetByStripeCouponIdAsync(coupon.Id);

        if (discount is null)
        {
            logger.LogInformation("Received coupon.deleted event for coupon {CouponId} not found in database. Ignoring.", coupon.Id);
            return;
        }

        await subscriptionDiscountRepository.DeleteAsync(discount);
        logger.LogInformation("Deleted subscription discount for Stripe coupon {CouponId}.", coupon.Id);
    }
}
