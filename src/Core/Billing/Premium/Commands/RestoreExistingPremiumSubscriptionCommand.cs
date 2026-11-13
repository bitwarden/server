using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Stripe;
using static Bit.Core.Billing.Constants.StripeConstants;

namespace Bit.Core.Billing.Premium.Commands;

public interface IRestoreExistingPremiumSubscriptionCommand
{
    /// <summary>
    /// Finds a Premium subscription for the user on their Stripe customer that has not ended. When it is active or
    /// trialing, turns Premium on for the user from it and links it to the user.
    /// </summary>
    /// <returns>
    /// The user's newest active or trialing subscription, otherwise their newest subscription whose status is neither
    /// canceled nor incomplete_expired, or <c>null</c> when there is none.
    /// </returns>
    Task<Subscription?> Run(User user);
}

public class RestoreExistingPremiumSubscriptionCommand(
    IStripeAdapter stripeAdapter,
    IPricingClient pricingClient,
    IUserService userService,
    IPushNotificationService pushNotificationService) : IRestoreExistingPremiumSubscriptionCommand
{
    public const string UnpaidSubscriptionMessage =
        "You have an unpaid Premium subscription. Update your payment method to restore Premium.";

    public async Task<Subscription?> Run(User user)
    {
        if (string.IsNullOrEmpty(user.GatewayCustomerId))
        {
            return null;
        }

        var subscriptions = await stripeAdapter.ListSubscriptionsAsync(new SubscriptionListOptions
        {
            Customer = user.GatewayCustomerId,
            Limit = 100
        });

        var unended = subscriptions.Data.Where(subscription =>
            subscription.Status is not (SubscriptionStatus.Canceled or SubscriptionStatus.IncompleteExpired) &&
            subscription.Metadata != null &&
            subscription.Metadata.TryGetValue(MetadataKeys.UserId, out var userId) &&
            Guid.TryParse(userId, out var parsedUserId) &&
            parsedUserId == user.Id).ToList();

        // A past_due subscription still grants Premium but has a payment outstanding, so it is not restored from.
        var existingSubscription =
            unended.FirstOrDefault(subscription => subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing) ??
            unended.FirstOrDefault();

        if (existingSubscription is { Status: SubscriptionStatus.Active or SubscriptionStatus.Trialing })
        {
            user.Premium = true;
            user.PremiumExpirationDate = existingSubscription.GetCurrentPeriodEnd();
            user.Gateway = GatewayType.Stripe;
            user.GatewaySubscriptionId = existingSubscription.Id;
            user.MaxStorageGb ??= (short)(await pricingClient.GetAvailablePremiumPlan()).Storage.Provided;
            user.LicenseKey = string.IsNullOrWhiteSpace(user.LicenseKey) ? CoreHelpers.SecureRandomString(20) : user.LicenseKey;
            user.RevisionDate = user.AccountRevisionDate = DateTime.UtcNow;
            await userService.SaveUserAsync(user);
            await pushNotificationService.PushSyncVaultAsync(user.Id);
        }

        return existingSubscription;
    }
}
