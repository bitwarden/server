using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Stripe;

namespace Bit.Billing.Services.Implementations;

public class CheckoutSessionCompletedHandler(
    IStripeEventService stripeEventService,
    IStripeEventUtilityService stripeEventUtilityService,
    IStripeAdapter stripeAdapter,
    IUserRepository userRepository,
    IPricingClient pricingClient,
    IPushNotificationAdapter pushNotificationAdapter,
    ILogger<CheckoutSessionCompletedHandler> logger)
    : ICheckoutSessionCompletedHandler
{
    public async Task HandleAsync(Event parsedEvent)
    {
        var session = await stripeEventService.GetCheckoutSession(parsedEvent, true);

        if (string.IsNullOrWhiteSpace(session.SubscriptionId))
        {
            logger.LogWarning("Checkout Session {SessionId} has no subscription ID", session.Id);
            return;
        }

        var subscription = await stripeAdapter.GetSubscriptionAsync(session.SubscriptionId);
        if (subscription is null)
        {
            logger.LogError("Subscription {SubscriptionId} not found for Checkout Session {SessionId}",
                session.SubscriptionId, session.Id);
            return;
        }

        var (_, userId, _) = stripeEventUtilityService.GetIdsFromMetadata(subscription.Metadata);
        if (!userId.HasValue)
        {
            logger.LogWarning("No userId found in metadata for subscription {SubscriptionId}", subscription.Id);
            return;
        }

        var user = await userRepository.GetByIdAsync(userId.Value);
        if (user is null)
        {
            logger.LogError("User {UserId} not found for subscription {SubscriptionId}", userId.Value, subscription.Id);
            return;
        }

        var premiumPlan = await pricingClient.GetAvailablePremiumPlan();

        // if the subscription does not contain the premium seat, this is not a premium subscription upgrade
        if (subscription.Items.All(i => i.Price.Id != premiumPlan.Seat.StripePriceId))
        {
            logger.LogError("Subscription {SubscriptionId} does not contain premium seat", subscription.Id);
            return;
        }

        user.Premium = true;
        user.GatewaySubscriptionId = subscription.Id;
        user.Gateway = GatewayType.Stripe;
        user.PremiumExpirationDate = subscription.GetCurrentPeriodEnd();
        user.MaxStorageGb = (short)premiumPlan.Storage.Provided;
        user.LicenseKey = string.IsNullOrWhiteSpace(user.LicenseKey) ? CoreHelpers.SecureRandomString(20) : user.LicenseKey;
        user.RevisionDate = DateTime.UtcNow;

        await userRepository.ReplaceAsync(user);
        await pushNotificationAdapter.NotifyPremiumStatusChangedAsync(user);
    }
}
