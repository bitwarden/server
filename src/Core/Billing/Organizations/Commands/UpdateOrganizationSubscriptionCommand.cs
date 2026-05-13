using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Tax.Utilities;
using Microsoft.Extensions.Logging;
using OneOf;
using Stripe;

namespace Bit.Core.Billing.Organizations.Commands;

using static StripeConstants;

/// <summary>
/// Updates an organization's Stripe subscription based on a set of changes described by an
/// <see cref="OrganizationSubscriptionChangeSet"/>. Handles adding, removing, and updating
/// subscription items as well as proration, invoice finalization, and tax exemption reconciliation.
/// </summary>
public interface IUpdateOrganizationSubscriptionCommand
{
    /// <summary>
    /// Applies the provided <paramref name="changeSet"/> to the organization's Stripe subscription.
    /// </summary>
    /// <param name="organization">The organization whose subscription will be updated.</param>
    /// <param name="changeSet">The set of changes to apply to the subscription.</param>
    /// <returns>
    /// A <see cref="BillingCommandResult{T}"/> containing the updated <see cref="Subscription"/>
    /// on success, or an error result if validation or the Stripe operation fails.
    /// </returns>
    Task<BillingCommandResult<Subscription>> Run(
        Organization organization,
        OrganizationSubscriptionChangeSet changeSet);
}

public class UpdateOrganizationSubscriptionCommand(
    ILogger<UpdateOrganizationSubscriptionCommand> logger,
    IStripeAdapter stripeAdapter) : BaseBillingCommand<UpdateOrganizationSubscriptionCommand>(logger), IUpdateOrganizationSubscriptionCommand
{
    private static readonly List<string> _validSubscriptionStatusesForUpdate =
    [
        SubscriptionStatus.Trialing,
        SubscriptionStatus.Active,
        SubscriptionStatus.PastDue
    ];

    private readonly ILogger<UpdateOrganizationSubscriptionCommand> _logger = logger;

    protected override Conflict DefaultConflict =>
        new("We had a problem updating your subscription. Please contact support for assistance.");

    public Task<BillingCommandResult<Subscription>> Run(
        Organization organization,
        OrganizationSubscriptionChangeSet changeSet) => HandleAsync<Subscription>(async () =>
    {
        var subscription = await FetchSubscriptionAsync(organization);

        if (subscription is null)
        {
            return new BadRequest("We couldn't find your subscription.");
        }

        if (!_validSubscriptionStatusesForUpdate.Contains(subscription.Status))
        {
            _logger.LogWarning(
                "{Command}: Tried to update organization ({OrganizationId}) subscription ({SubscriptionId}) with status ({SubscriptionStatus})",
                CommandName, organization.Id, subscription.Id, subscription.Status);
            return new BadRequest("Your subscription cannot be updated in its current status.");
        }

        if (changeSet.Changes.Count == 0)
        {
            _logger.LogWarning(
                "{Command}: Change set for organization ({OrganizationId}) subscription ({SubscriptionId}) contained zero changes",
                CommandName, organization.Id, subscription.Id);
            return new Conflict("No changes were provided for the organization subscription update");
        }

        await ReconcileTaxExemptionAsync(subscription.Customer);

        var hasStructuralChanges = changeSet.ChargeImmediately;
        var isChargedAutomatically = subscription.CollectionMethod == CollectionMethod.ChargeAutomatically;
        var isBilledAnnually = subscription.Items.FirstOrDefault()?.Price.Recurring?.Interval == Intervals.Year;

        var prorationBehavior =
            hasStructuralChanges ? ProrationBehavior.AlwaysInvoice : ProrationBehavior.CreateProrations;
        var paymentBehavior =
            hasStructuralChanges && isChargedAutomatically ? PaymentBehavior.PendingIfIncomplete : null;

        var items = new List<SubscriptionItemOptions>();
        foreach (var change in changeSet.Changes)
        {
            var validationResult = change.Match(
                addItem => ValidateItemAddition(addItem, subscription),
                changeItemPrice => ValidateItemPriceChange(changeItemPrice, subscription),
                removeItem => ValidateItemRemoval(removeItem, subscription),
                updateItemQuantity => ValidateItemQuantityUpdate(updateItemQuantity, subscription));

            if (validationResult.IsT1)
            {
                return validationResult.AsT1;
            }

            items.Add(validationResult.AsT0);
        }

        var schedules = await stripeAdapter.ListSubscriptionSchedulesAsync(
            new SubscriptionScheduleListOptions { Customer = subscription.CustomerId });
        var activeSchedule = schedules.Data.FirstOrDefault(s =>
            s.Status == SubscriptionScheduleStatus.Active && s.SubscriptionId == subscription.Id);

        /* An active schedule here means PriceIncreaseScheduler created a schedule to defer a
         * Families price migration to renewal. A 2-phase schedule is the standard migration
         * state; a 1-phase schedule means the subscription was cancelled (end-of-period) while
         * a schedule was attached (PM-33897). Either way, we update via the schedule to avoid
         * conflicting with Stripe's schedule ownership of the subscription. */
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
            var now = subscription.TestClock?.FrozenTime ?? DateTime.UtcNow;

            /* This applies the change set's price IDs (which are Phase 1 / current-plan prices)
             * to all active phases. This works because storage prices are uniform across the
             * Families migration. If storage prices ever differ between phases, both this command
             * and UpdatePremiumStorageCommand would need plan-aware price resolution (e.g. matching
             * Phase 2's seat price to determine the correct storage price). */
            var phases = new List<SubscriptionSchedulePhaseOptions>();

            // Stripe rejects schedule updates that include phases whose end_date is in the past.
            // A phase ending at exactly `now` has effectively ended (strict > is intentional).
            if (phase1.EndDate > now)
            {
                phases.Add(new SubscriptionSchedulePhaseOptions
                {
                    StartDate = phase1.StartDate,
                    EndDate = phase1.EndDate,
                    Items = ApplyChangesToPhaseItems(phase1.Items, changeSet.Changes),
                    Discounts = phase1.Discounts?.Select(d =>
                        new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.CouponId }).ToList(),
                    ProrationBehavior = phase1.ProrationBehavior
                });
            }
            else
            {
                _logger.LogWarning(
                    "{Command}: Phase 1 has already ended (EndDate: {EndDate}), updating only active phase(s)",
                    CommandName, phase1.EndDate);
            }

            var phase1Ended = phase1.EndDate <= now;

            if (activeSchedule.Phases.Count >= 2)
            {
                var phase2 = activeSchedule.Phases[1];
                phases.Add(new SubscriptionSchedulePhaseOptions
                {
                    StartDate = phase2.StartDate,
                    EndDate = phase2.EndDate,
                    Items = ApplyChangesToPhaseItems(phase2.Items, changeSet.Changes),
                    // When Phase 2 is already active, its one-time migration discount has been
                    // applied and consumed. Re-including it would cause Stripe to re-apply it.
                    Discounts = phase1Ended
                        ? []
                        : phase2.Discounts?.Select(d =>
                            new SubscriptionSchedulePhaseDiscountOptions { Coupon = d.CouponId }).ToList(),
                    ProrationBehavior = phase2.ProrationBehavior
                });
            }

            if (phases.Count == 0)
            {
                _logger.LogWarning(
                    "{Command}: Schedule ({ScheduleId}) has no updatable phases remaining",
                    CommandName, activeSchedule.Id);
                return DefaultConflict;
            }

            /* Note: the schedule phase API does not support PendingInvoiceItemInterval. For annual
             * subscribers, the non-schedule path invoices prorations monthly. When the top-level
             * ProrationBehavior is AlwaysInvoice (structural changes), Stripe invoices immediately.
             * When it is CreateProrations (non-structural changes), prorations remain pending until
             * the next invoice (~15 days). Accepted trade-off for the migration window. */
            await stripeAdapter.UpdateSubscriptionScheduleAsync(activeSchedule.Id,
                new SubscriptionScheduleUpdateOptions
                {
                    EndBehavior = activeSchedule.EndBehavior,
                    Phases = phases,
                    ProrationBehavior = prorationBehavior
                });

            /* Note: this returns the pre-update subscription. The schedule update modified the
             * subscription via Stripe, but we don't re-fetch it. Callers currently only check
             * success/failure. If a caller ever needs the post-update state, re-fetch here. */
            return subscription;
        }

        var options = new SubscriptionUpdateOptions { Items = items, ProrationBehavior = prorationBehavior };

        if (paymentBehavior is not null)
        {
            options.PaymentBehavior = paymentBehavior;
        }

        if (isBilledAnnually && !hasStructuralChanges && subscription.Status != SubscriptionStatus.Trialing)
        {
            options.PendingInvoiceItemInterval = new SubscriptionPendingInvoiceItemIntervalOptions
            {
                Interval = Intervals.Month
            };
        }

        var updatedSubscription = await stripeAdapter.UpdateSubscriptionAsync(subscription.Id, options);

        // ReSharper disable once InvertIf
        if (!isChargedAutomatically && hasStructuralChanges && updatedSubscription.LatestInvoiceId is not null)
        {
            var invoice = await stripeAdapter.GetInvoiceAsync(updatedSubscription.LatestInvoiceId);

            if (invoice is { Status: InvoiceStatus.Draft })
            {
                var finalizedInvoice = await stripeAdapter.FinalizeInvoiceAsync(invoice.Id,
                    new InvoiceFinalizeOptions { AutoAdvance = false });

                await stripeAdapter.SendInvoiceAsync(finalizedInvoice.Id);
            }
            else
            {
                _logger.LogWarning(
                    "{Command}: Latest invoice ({InvoiceId}) after subscription ({SubscriptionId}) update for organization ({OrganizationId}) was in '{Status}' status",
                    CommandName, invoice.Id, subscription.Id, organization.Id, invoice.Status);
            }
        }

        return updatedSubscription;
    });

    private async Task<Subscription?> FetchSubscriptionAsync(Organization organization)
    {
        try
        {
            return await stripeAdapter.GetSubscriptionAsync(organization.GatewaySubscriptionId, new SubscriptionGetOptions
            {
                Expand = ["customer", "test_clock"]
            });
        }
        catch (StripeException stripeException) when (stripeException.StripeError?.Code == ErrorCodes.ResourceMissing)
        {
            _logger.LogError("{Command}: Subscription ({SubscriptionId}) for Organization ({OrganizationId}) was not found",
                CommandName, organization.GatewaySubscriptionId, organization.Id);
            return null;
        }
    }

    private async Task ReconcileTaxExemptionAsync(Customer customer)
    {
        var determinedTaxExemptStatus = TaxHelpers.DetermineTaxExemptStatus(customer.Address?.Country, customer.TaxExempt);
        switch (customer)
        {
            case { Address.Country: not null and not "", TaxExempt: var customerTaxExemptStatus }
                when determinedTaxExemptStatus != customerTaxExemptStatus:
                await stripeAdapter.UpdateCustomerAsync(customer.Id,
                    new CustomerUpdateOptions { TaxExempt = determinedTaxExemptStatus });
                break;
        }

    }

    private static OneOf<SubscriptionItemOptions, BadRequest> ValidateItemAddition(
        AddItem addItem, Subscription subscription)
    {
        var duplicate = subscription.Items.Data
            .FirstOrDefault(i => i.Price.Id == addItem.PriceId);

        if (duplicate is not null)
        {
            return new BadRequest($"Subscription already contains an item with price '{addItem.PriceId}'.");
        }

        return new SubscriptionItemOptions
        {
            Price = addItem.PriceId,
            Quantity = addItem.Quantity
        };
    }

    private static OneOf<SubscriptionItemOptions, BadRequest> ValidateItemPriceChange(
        ChangeItemPrice priceChange, Subscription subscription)
    {
        var currentItem = subscription.Items.Data
            .FirstOrDefault(i => i.Price.Id == priceChange.CurrentPriceId);

        if (currentItem is null)
        {
            return new BadRequest($"Subscription does not contain an item with price '{priceChange.CurrentPriceId}'.");
        }

        return new SubscriptionItemOptions
        {
            Id = currentItem.Id,
            Price = priceChange.UpdatedPriceId,
            Quantity = priceChange.Quantity ?? currentItem.Quantity
        };
    }

    private static OneOf<SubscriptionItemOptions, BadRequest> ValidateItemQuantityUpdate(
        UpdateItemQuantity updateItemQuantity, Subscription subscription)
    {
        var existingItem = subscription.Items.Data
            .FirstOrDefault(i => i.Price.Id == updateItemQuantity.PriceId);

        if (existingItem is null)
        {
            return new BadRequest($"Subscription does not contain an item with price '{updateItemQuantity.PriceId}'.");
        }

        return updateItemQuantity.Quantity == 0
            ? new SubscriptionItemOptions { Id = existingItem.Id, Deleted = true }
            : new SubscriptionItemOptions { Id = existingItem.Id, Price = updateItemQuantity.PriceId, Quantity = updateItemQuantity.Quantity };
    }

    private static OneOf<SubscriptionItemOptions, BadRequest> ValidateItemRemoval(
        RemoveItem removeItem, Subscription subscription)
    {
        var existingItem = subscription.Items.Data
            .FirstOrDefault(i => i.Price.Id == removeItem.PriceId);

        if (existingItem is null)
        {
            return new BadRequest($"Subscription does not contain an item with price '{removeItem.PriceId}'.");
        }

        return new SubscriptionItemOptions
        {
            Id = existingItem.Id,
            Deleted = true
        };
    }

    private static List<SubscriptionSchedulePhaseItemOptions> ApplyChangesToPhaseItems(
        IList<SubscriptionSchedulePhaseItem> phaseItems,
        IReadOnlyList<OrganizationSubscriptionChange> changes)
    {
        /* Note: when a change targets a price ID not present in this phase (e.g. Phase 2 has
         * migrated prices), the change is silently skipped. This is safe because subscription-
         * level validation (ValidateItemAddition, ValidateItemPriceChange, etc.) already ran
         * before this method is called. */
        var items = phaseItems
            .Select(i => new SubscriptionSchedulePhaseItemOptions { Price = i.PriceId, Quantity = i.Quantity })
            .ToList();

        foreach (var change in changes)
        {
            change.Switch(
                addItem => items.Add(new SubscriptionSchedulePhaseItemOptions
                {
                    Price = addItem.PriceId,
                    Quantity = addItem.Quantity
                }),
                changeItemPrice =>
                {
                    var existing = items.FirstOrDefault(i => i.Price == changeItemPrice.CurrentPriceId);
                    if (existing != null)
                    {
                        existing.Price = changeItemPrice.UpdatedPriceId;
                        if (changeItemPrice.Quantity.HasValue)
                        {
                            existing.Quantity = changeItemPrice.Quantity.Value;
                        }
                    }
                },
                removeItem => items.RemoveAll(i => i.Price == removeItem.PriceId),
                updateItemQuantity =>
                {
                    if (updateItemQuantity.Quantity == 0)
                    {
                        items.RemoveAll(i => i.Price == updateItemQuantity.PriceId);
                    }
                    else
                    {
                        var existing = items.FirstOrDefault(i => i.Price == updateItemQuantity.PriceId);
                        if (existing != null)
                        {
                            existing.Quantity = updateItemQuantity.Quantity;
                        }
                        else
                        {
                            items.Add(new SubscriptionSchedulePhaseItemOptions
                            {
                                Price = updateItemQuantity.PriceId,
                                Quantity = updateItemQuantity.Quantity
                            });
                        }
                    }
                });
        }

        return items;
    }
}
