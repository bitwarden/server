using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using OneOf.Types;
using Stripe;

namespace Bit.Core.Billing.Premium.Commands;

using static StripeConstants;

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
    /// <returns>A billing command result indicating success or failure.</returns>
    Task<BillingCommandResult<None>> Run(User user, short additionalStorageGb);
}

public class UpdatePremiumStorageCommand(
    IBraintreeService braintreeService,
    IStripeAdapter stripeAdapter,
    IUserService userService,
    IPricingClient pricingClient,
    ILogger<UpdatePremiumStorageCommand> logger)
    : BaseBillingCommand<UpdatePremiumStorageCommand>(logger), IUpdatePremiumStorageCommand
{
    private readonly ILogger<UpdatePremiumStorageCommand> _logger = logger;

    public Task<BillingCommandResult<None>> Run(User user, short additionalStorageGb) => HandleAsync<None>(async () =>
    {
        if (user is not { Premium: true, GatewaySubscriptionId: not null and not "" })
        {
            return new BadRequest("User does not have a premium subscription.");
        }

        if (!user.MaxStorageGb.HasValue)
        {
            return new BadRequest("User has no access to storage.");
        }

        // Fetch all premium plans and the user's subscription to find which plan they're on
        var premiumPlans = await pricingClient.ListPremiumPlans();
        var subscription = await stripeAdapter.GetSubscriptionAsync(user.GatewaySubscriptionId, new SubscriptionGetOptions
        {
            Expand = ["customer"]
        });

        // Find the password manager subscription item (seat, not storage) and match it to a plan
        var passwordManagerItem = subscription.Items.Data.FirstOrDefault(i =>
            premiumPlans.Any(p => p.Seat.StripePriceId == i.Price.Id));

        if (passwordManagerItem == null)
        {
            return new Conflict("Premium subscription does not have a Password Manager line item.");
        }

        var premiumPlan = premiumPlans.First(p => p.Seat.StripePriceId == passwordManagerItem.Price.Id);

        var baseStorageGb = (short)premiumPlan.Storage.Provided;

        if (additionalStorageGb < 0)
        {
            return new BadRequest("Additional storage cannot be negative.");
        }

        var maxStorageGb = (short)(baseStorageGb + additionalStorageGb);

        if (maxStorageGb > 100)
        {
            return new BadRequest("Maximum storage is 100 GB.");
        }

        // Idempotency check: if user already has the requested storage, return success
        if (user.MaxStorageGb == maxStorageGb)
        {
            return new None();
        }

        var remainingStorage = user.StorageBytesRemaining(maxStorageGb);
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

        var usingPayPal = subscription.Customer.Metadata.ContainsKey(MetadataKeys.BraintreeCustomerId);

        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });
        var activeSchedule = schedules.Data.FirstOrDefault(s =>
            s.Status == SubscriptionScheduleStatus.Active && s.SubscriptionId == subscription.Id);

        // An active schedule here means PriceIncreaseScheduler created a schedule to defer a
        // Premium price migration to renewal. A 2-phase schedule is the standard migration state;
        // a 1-phase schedule means the subscription was cancelled (end-of-period) while a schedule
        // was attached (PM-33897). Either way, we update via the schedule to avoid conflicting
        // with Stripe's schedule ownership of the subscription.
        if (activeSchedule is { Phases.Count: > 0 })
        {
            if (activeSchedule.Phases.Count > 2)
            {
                _logger.LogWarning(
                    "{Command}: Subscription schedule ({ScheduleId}) has {PhaseCount} phases (expected 1-2), only updating first two",
                    CommandName, activeSchedule.Id, activeSchedule.Phases.Count);
            }

            _logger.LogInformation(
                "{Command}: Active subscription schedule ({ScheduleId}) found for subscription ({SubscriptionId}), updating schedule phases",
                CommandName, activeSchedule.Id, subscription.Id);

            var phase1 = activeSchedule.Phases[0];

            // Storage prices are uniform across the Premium migration, so we use the same price
            // for both phases. If storage prices ever differ between phases, this would need
            // plan-aware price resolution (e.g. matching Phase 2's seat price to a plan).
            var storagePriceId = premiumPlan.Storage.StripePriceId;

            var phases = new List<SubscriptionSchedulePhaseOptions>
            {
                new()
                {
                    StartDate = phase1.StartDate,
                    EndDate = phase1.EndDate,
                    Items = BuildPhaseItemsWithStorage(phase1.Items, storagePriceId, additionalStorageGb),
                    Discounts = phase1.Discounts?.Select(d =>
                        new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.CouponId }).ToList(),
                    ProrationBehavior = phase1.ProrationBehavior
                }
            };

            if (activeSchedule.Phases.Count >= 2)
            {
                var phase2 = activeSchedule.Phases[1];
                phases.Add(new SubscriptionSchedulePhaseOptions
                {
                    StartDate = phase2.StartDate,
                    EndDate = phase2.EndDate,
                    Items = BuildPhaseItemsWithStorage(phase2.Items, storagePriceId, additionalStorageGb),
                    Discounts = phase2.Discounts?.Select(d =>
                        new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.CouponId }).ToList(),
                    ProrationBehavior = phase2.ProrationBehavior
                });
            }

            await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
                new SubscriptionScheduleUpdateOptions
                {
                    EndBehavior = activeSchedule.EndBehavior,
                    Phases = phases,
                    ProrationBehavior = usingPayPal
                        ? ProrationBehavior.CreateProrations
                        : ProrationBehavior.AlwaysInvoice
                });
        }
        else
        {
            // CreateProrations for PayPal: pending proration items are picked up by the manual invoice below.
            // AlwaysInvoice for card: Stripe generates and charges the proration invoice automatically.
            await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, new SubscriptionUpdateOptions
            {
                Items = subscriptionItemOptions,
                ProrationBehavior = usingPayPal
                    ? ProrationBehavior.CreateProrations
                    : ProrationBehavior.AlwaysInvoice
            });
        }

        if (usingPayPal)
        {
            var draftInvoice = await stripeAdapter.CreateInvoiceAsync(new InvoiceCreateOptions
            {
                Customer = subscription.CustomerId,
                Subscription = subscription.Id,
                AutoAdvance = false,
                CollectionMethod = CollectionMethod.ChargeAutomatically
            });

            var finalizedInvoice = await stripeAdapter.FinalizeInvoiceAsync(draftInvoice.Id,
                new InvoiceFinalizeOptions { AutoAdvance = false, Expand = ["customer"] });

            await braintreeService.PayInvoice(new UserId(user.Id), finalizedInvoice);
        }

        // Update the user's max storage
        user.MaxStorageGb = maxStorageGb;
        await userService.SaveUserAsync(user);

        return new None();
    });

    private static List<SubscriptionSchedulePhaseItemOptions> BuildPhaseItemsWithStorage(
        IList<SubscriptionSchedulePhaseItem> phaseItems,
        string storagePriceId,
        short additionalStorageGb)
    {
        var items = phaseItems
            .Where(i => i.PriceId != storagePriceId)
            .Select(i => new SubscriptionSchedulePhaseItemOptions { Price = i.PriceId, Quantity = i.Quantity })
            .ToList();

        if (additionalStorageGb > 0)
        {
            items.Add(new SubscriptionSchedulePhaseItemOptions
            {
                Price = storagePriceId,
                Quantity = additionalStorageGb
            });
        }

        return items;
    }
}
