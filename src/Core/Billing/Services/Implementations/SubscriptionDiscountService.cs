using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations;

public class SubscriptionDiscountService(
    ISubscriptionDiscountRepository subscriptionDiscountRepository,
    IStripeAdapter stripeAdapter,
    IPricingClient pricingClient) : ISubscriptionDiscountService
{
    public async Task<bool> ValidateDiscountForUserAsync(User user, string stripeCouponId, DiscountAudienceType expectedAudienceType)
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

        if (discount.AudienceType != expectedAudienceType)
        {
            return false;
        }

        return discount.AudienceType switch
        {
            DiscountAudienceType.UserHasNoPreviousSubscriptions =>
                await UserHasNoPreviousSubscriptionsAsync(user),
            _ => false
        };
    }

    private async Task<bool> UserHasNoPreviousSubscriptionsAsync(User user)
    {
        // Check current premium status
        if (user.Premium || !string.IsNullOrEmpty(user.GatewaySubscriptionId))
        {
            return false;
        }

        // If user has no Stripe customer, they can't have had past subscriptions
        if (string.IsNullOrEmpty(user.GatewayCustomerId))
        {
            return true;
        }

        // Get all premium plans (including legacy) from pricing service
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var premiumPriceIds = premiumPlans.Select(p => p.Seat.StripePriceId).ToHashSet();

        // Check for any past premium subscriptions in Stripe
        var subscriptions = await stripeAdapter.ListSubscriptionsAsync(new SubscriptionListOptions
        {
            Customer = user.GatewayCustomerId,
            Expand = ["data.items.data.price"]
        });

        // Check if any subscription contains premium price IDs
        foreach (var subscription in subscriptions.Data)
        {
            if (subscription.Items.Data.Any(item => premiumPriceIds.Contains(item.Price.Id)))
            {
                return false;
            }
        }

        return true;
    }
}
