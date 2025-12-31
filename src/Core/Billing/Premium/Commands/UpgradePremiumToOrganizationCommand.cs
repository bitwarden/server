using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;

namespace Bit.Core.Billing.Premium.Commands;
/// <summary>
/// Upgrades a user's Premium subscription to an Organization plan by modifying the existing Stripe subscription.
/// </summary>
public interface IUpgradePremiumToOrganizationCommand
{
    /// <summary>
    /// Upgrades a Premium subscription to an Organization subscription.
    /// </summary>
    /// <param name="user">The user with an active Premium subscription to upgrade.</param>
    /// <param name="targetPlanType">The target organization plan type to upgrade to.</param>
    /// <param name="seats">The number of seats for the organization plan.</param>
    /// <param name="premiumAccess">Whether to include premium access for the organization.</param>
    /// <param name="storage">Additional storage in GB for the organization.</param>
    /// <param name="trialEndDate">The trial end date to apply to the upgraded subscription.</param>
    /// <returns>A billing command result indicating success or failure with appropriate error details.</returns>
    Task<BillingCommandResult<None>> Run(
        User user,
        PlanType targetPlanType,
        int seats,
        bool premiumAccess,
        int? storage,
        DateTime? trialEndDate);
}

public class UpgradePremiumToOrganizationCommand(
    ILogger<UpgradePremiumToOrganizationCommand> logger,
    IPricingClient pricingClient,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    IUserService userService)
    : BaseBillingCommand<UpgradePremiumToOrganizationCommand>(logger), IUpgradePremiumToOrganizationCommand
{
    public Task<BillingCommandResult<None>> Run(
        User user,
        PlanType targetPlanType,
        int seats,
        bool premiumAccess,
        int? storage,
        DateTime? trialEndDate) => HandleAsync<None>(async () =>
    {
        // Validate that the user has an active Premium subscription
        if (!user.Premium)
        {
            return new BadRequest("User does not have an active Premium subscription.");
        }

        if (string.IsNullOrEmpty(user.GatewaySubscriptionId))
        {
            return new BadRequest("User does not have a Stripe subscription.");
        }

        if (seats < 1)
        {
            return new BadRequest("Seats must be at least 1.");
        }

        if (trialEndDate.HasValue && trialEndDate.Value < DateTime.UtcNow)
        {
            return new BadRequest("Trial end date cannot be in the past.");
        }

        // Fetch the current Premium subscription from Stripe
        var currentSubscription = await subscriberService.GetSubscriptionOrThrow(user, new SubscriptionGetOptions
        {
            Expand = ["items.data.price"]
        });

        // Get the target organization plan
        var targetPlan = await pricingClient.GetPlanOrThrow(targetPlanType);

        // Validate plan supports requested features
        if (premiumAccess && string.IsNullOrEmpty(targetPlan.PasswordManager.StripePremiumAccessPlanId))
        {
            return new BadRequest("The selected plan does not support premium access.");
        }

        if (storage is > 0 && string.IsNullOrEmpty(targetPlan.PasswordManager.StripeStoragePlanId))
        {
            return new BadRequest("The selected plan does not support additional storage.");
        }

        // Build the list of subscription item updates
        var subscriptionItemOptions = new List<SubscriptionItemOptions>();

        // Mark existing Premium subscription items for deletion
        foreach (var item in currentSubscription.Items.Data)
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Id = item.Id,
                Deleted = true
            });
        }

        // Add new organization subscription items
        if (targetPlan.HasNonSeatBasedPasswordManagerPlan())
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = targetPlan.PasswordManager.StripePlanId,
                Quantity = 1
            });
        }
        else
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = targetPlan.PasswordManager.StripeSeatPlanId,
                Quantity = seats
            });
        }

        if (premiumAccess)
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = targetPlan.PasswordManager.StripePremiumAccessPlanId,
                Quantity = 1
            });
        }

        if (storage is > 0)
        {
            subscriptionItemOptions.Add(new SubscriptionItemOptions
            {
                Price = targetPlan.PasswordManager.StripeStoragePlanId,
                Quantity = storage
            });
        }

        // Build the subscription update options
        var subscriptionUpdateOptions = new SubscriptionUpdateOptions
        {
            Items = subscriptionItemOptions,
            ProrationBehavior = StripeConstants.ProrationBehavior.None,
            Metadata = new Dictionary<string, string>(currentSubscription.Metadata ?? new Dictionary<string, string>())
            {
                ["premium_upgrade_metadata"] = currentSubscription.Items.Data
                    .Select(item => item.Price.Id)
                    .FirstOrDefault() ?? "premium"
            }
        };

        // Apply trial period if specified
        if (trialEndDate.HasValue)
        {
            subscriptionUpdateOptions.TrialEnd = trialEndDate.Value;
        }

        // Update the subscription in Stripe
        await stripeAdapter.UpdateSubscriptionAsync(currentSubscription.Id, subscriptionUpdateOptions);

        // Update user record
        user.PremiumExpirationDate = trialEndDate ?? currentSubscription.GetCurrentPeriodEnd();
        user.RevisionDate = DateTime.UtcNow;
        await userService.SaveUserAsync(user);

        return new None();
    });
}
