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
        SubscriptionStatus.Trialing, SubscriptionStatus.Active, SubscriptionStatus.PastDue
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

        var hasStructuralChanges = changeSet.Changes.Any(change => change.IsStructural);
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
                Expand = ["customer"]
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
}
