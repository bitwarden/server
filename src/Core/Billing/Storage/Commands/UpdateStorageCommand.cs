using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Storage.Commands;

/// <summary>
/// Updates the storage allocation for a premium user's subscription.
/// Handles both increases and decreases in storage in an idempotent manner.
/// </summary>
public interface IUpdateStorageCommand
{
    /// <summary>
    /// Updates the user's storage to the specified amount.
    /// </summary>
    /// <param name="user">The premium user whose storage should be updated.</param>
    /// <param name="storageGb">The desired total storage amount in GB (must be between base storage and 100 GB).</param>
    /// <returns>A billing command result with payment intent client secret if payment is required.</returns>
    Task<BillingCommandResult<string?>> Run(User user, short storageGb);
}

public class UpdateStorageCommand(
    IStripePaymentService paymentService,
    IUserService userService,
    IPricingClient pricingClient,
    ILogger<UpdateStorageCommand> logger)
    : BaseBillingCommand<UpdateStorageCommand>(logger), IUpdateStorageCommand
{
    public Task<BillingCommandResult<string?>> Run(User user, short storageGb) => HandleAsync<string?>(async () =>
    {
        if (user == null)
        {
            return new BadRequest("User not found.");
        }

        if (!user.Premium)
        {
            return new BadRequest("User does not have a premium subscription.");
        }

        if (string.IsNullOrWhiteSpace(user.GatewayCustomerId))
        {
            return new BadRequest("No payment method found.");
        }

        if (string.IsNullOrWhiteSpace(user.GatewaySubscriptionId))
        {
            return new BadRequest("No subscription found.");
        }

        var premiumPlan = await pricingClient.GetAvailablePremiumPlan();
        var baseStorageGb = (short)premiumPlan.Storage.Provided;

        if (storageGb < baseStorageGb)
        {
            return new BadRequest($"Storage cannot be less than the base amount of {baseStorageGb} GB.");
        }

        if (storageGb > 100)
        {
            return new BadRequest("Maximum storage is 100 GB.");
        }

        // Check if the requested storage would fit the user's current usage
        if (!user.MaxStorageGb.HasValue)
        {
            return new BadRequest("No access to storage.");
        }

        // Idempotency check: if user already has the requested storage, return success
        if (user.MaxStorageGb == storageGb)
        {
            return (string?)null; // No payment intent needed for no-op
        }

        var remainingStorage = user.StorageBytesRemaining(storageGb);
        if (remainingStorage < 0)
        {
            return new BadRequest(
                $"You are currently using {CoreHelpers.ReadableBytesSize(user.Storage.GetValueOrDefault(0))} of storage. " +
                "Delete some stored data first.");
        }

        // Calculate the additional storage beyond base
        var additionalStorage = storageGb - baseStorageGb;

        // Call the payment service to adjust the subscription
        var paymentIntentClientSecret = await paymentService.AdjustStorageAsync(
            user,
            additionalStorage,
            premiumPlan.Storage.StripePriceId);

        // Update the user's max storage
        user.MaxStorageGb = storageGb;
        await userService.SaveUserAsync(user);

        return paymentIntentClientSecret;
    });
}
