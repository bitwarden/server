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
    IUserService userService,
    IPricingClient pricingClient,
    ILogger<UpdatePremiumStorageCommand> logger)
    : BaseBillingCommand<UpdatePremiumStorageCommand>(logger), IUpdatePremiumStorageCommand
{
    public Task<BillingCommandResult<string?>> Run(User user, short additionalStorageGb) => HandleAsync<string?>(async () =>
    {
        if (!user.Premium)
        {
            return new BadRequest("User does not have a premium subscription.");
        }

        if (!user.MaxStorageGb.HasValue)
        {
            return new BadRequest("No access to storage.");
        }

        // Fetch all premium plans and the user's subscription to find which plan they're on
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var subscription = await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId);

        // Find the password manager subscription item (seat, not storage) and match it to a plan
        var passwordManagerItem = subscription.Items.Data.FirstOrDefault(i =>
            premiumPlans.Any(p => p.Seat.StripePriceId == i.Price.Id));

        if (passwordManagerItem == null)
        {
            return new BadRequest("Premium subscription item not found.");
        }

        var premiumPlan = premiumPlans.First(p => p.Seat.StripePriceId == passwordManagerItem.Price.Id);

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
    });
}
