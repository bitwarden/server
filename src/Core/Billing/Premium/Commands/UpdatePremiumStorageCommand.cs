using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Core.Billing.Premium.Commands;

/// <summary>
/// Updates the storage allocation for a premium user's subscription.
/// Handles both increases and decreases in storage in an idempotent manner.
/// </summary>
public interface IUpdatePremiumStorageCommand
{
    /// <summary>
    /// Updates the user's storage by the specified additional amount.
    /// </summary>
    /// <param name="user">The premium user whose storage should be updated.</param>
    /// <param name="additionalStorageGb">The additional storage amount in GB beyond base storage.</param>
    /// <returns>A billing command result with payment intent client secret if payment is required.</returns>
    Task<BillingCommandResult<string?>> Run(User user, short additionalStorageGb);
}

public class UpdatePremiumStorageCommand(
    IStripeAdapter stripeAdapter,
    IStripePaymentService paymentService,
    IUserService userService,
    IPricingClient pricingClient,
    IFeatureService featureService,
    ILogger<UpdatePremiumStorageCommand> logger)
    : BaseBillingCommand<UpdatePremiumStorageCommand>(logger), IUpdatePremiumStorageCommand
{
    public Task<BillingCommandResult<string?>> Run(User user, short additionalStorageGb) => HandleAsync<string?>(async () =>
    {
        if (!user.Premium)
        {
            return new BadRequest("User does not have a premium subscription.");
        }

        // Fetch all premium plans and find the one the user is on
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var premiumPlan = premiumPlans.FirstOrDefault(p => p.Available);

        if (premiumPlan == null)
        {
            return new BadRequest("No available premium plan found.");
        }

        var baseStorageGb = (short)premiumPlan.Storage.Provided;

        if (additionalStorageGb < 0)
        {
            return new BadRequest("Additional storage cannot be negative.");
        }

        var newTotalStorageGb = (short)(baseStorageGb + additionalStorageGb);

        if (newTotalStorageGb > 100)
        {
            return new BadRequest("Maximum storage is 100 GB.");
        }

        // Check if the requested storage would fit the user's current usage
        if (!user.MaxStorageGb.HasValue)
        {
            return new BadRequest("No access to storage.");
        }

        // Idempotency check: if user already has the requested storage, return success
        if (user.MaxStorageGb == newTotalStorageGb)
        {
            return (string?)null; // No payment intent needed for no-op
        }

        var remainingStorage = user.StorageBytesRemaining(newTotalStorageGb);
        if (remainingStorage < 0)
        {
            return new BadRequest(
                $"You are currently using {CoreHelpers.ReadableBytesSize(user.Storage.GetValueOrDefault(0))} of storage. " +
                "Delete some stored data first.");
        }

        // Check feature flag to determine which code path to use
        if (featureService.IsEnabled(FeatureFlagKeys.PM29594_UpdateIndividualSubscriptionPage))
        {
            // NEW PATH: Directly update the premium subscription with prorations
            // This is simpler than using FinalizeSubscriptionChangeAsync since storage is always billed annually
            var subscription = await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId);
            if (subscription == null)
            {
                return new BadRequest("Subscription not found.");
            }

            // Find the storage line item in the subscription
            var storageItem = subscription.Items.Data.FirstOrDefault(i => i.Price.Id == premiumPlan.Storage.StripePriceId);

            var subscriptionItemOptions = new List<SubscriptionItemOptions>();

            if (additionalStorageGb > 0)
            {
                if (storageItem != null)
                {
                    // Update existing storage item
                    subscriptionItemOptions.Add(new SubscriptionItemOptions
                    {
                        Id = storageItem.Id,
                        Price = premiumPlan.Storage.StripePriceId,
                        Quantity = additionalStorageGb
                    });
                }
                else
                {
                    // Add new storage item
                    subscriptionItemOptions.Add(new SubscriptionItemOptions
                    {
                        Price = premiumPlan.Storage.StripePriceId,
                        Quantity = additionalStorageGb
                    });
                }
            }
            else if (storageItem != null)
            {
                // Remove storage item if setting to 0
                subscriptionItemOptions.Add(new SubscriptionItemOptions
                {
                    Id = storageItem.Id,
                    Deleted = true
                });
            }

            // Update subscription with prorations
            // Storage is billed annually, so we create prorations and invoice immediately
            var subscriptionUpdateOptions = new SubscriptionUpdateOptions
            {
                Items = subscriptionItemOptions,
                ProrationBehavior = Core.Constants.CreateProrations
            };

            await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, subscriptionUpdateOptions);

            // Update the user's max storage
            user.MaxStorageGb = newTotalStorageGb;
            await userService.SaveUserAsync(user);

            // No payment intent needed - the subscription update will automatically create and finalize the invoice
            return (string?)null;
        }
        else
        {
            // OLD PATH: Use BillingHelpers.AdjustStorageAsync
            // Convert from additionalStorageGb (final additional beyond base) to storageAdjustmentGb (delta from current)
            var currentTotal = user.MaxStorageGb.Value;
            var desiredTotal = newTotalStorageGb;
            var adjustment = (short)(desiredTotal - currentTotal);

            var paymentIntentSecret = await BillingHelpers.AdjustStorageAsync(
                paymentService, user, adjustment, premiumPlan.Storage.StripePriceId, baseStorageGb);

            await userService.SaveUserAsync(user);
            return paymentIntentSecret;
        }
    });
}
