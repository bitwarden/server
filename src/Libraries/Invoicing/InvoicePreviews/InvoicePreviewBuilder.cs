using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews;

/// <summary>Projects a Stripe invoice or subscription into an <see cref="InvoicePreview"/>. Every projected amount is dollars.</summary>
internal sealed class InvoicePreviewBuilder(ILogger<InvoicePreviewBuilder> logger)
{
    /// <summary>Projects a preview invoice, including prorations, discounts, and tax, into an <see cref="InvoicePreview"/>.</summary>
    internal InvoicePreview Build(Invoice invoice, PlanTierType planTier, PlanCadenceType cadence)
    {
        var lineItemsByReference = new Dictionary<string, InvoicePreviewItem>();
        var prorationLines = new List<(string Reference, InvoiceLineItem Line)>();
        var discounts = DiscountMapper.Partition(invoice, logger);

        foreach (var line in invoice.Lines?.Data ?? [])
        {
            var price = line.Pricing?.PriceDetails?.Price;
            var reference = ResolvePurchasableReference(price);
            if (reference is null)
            {
                continue;
            }

            if (line.Parent?.SubscriptionItemDetails?.Proration == true)
            {
                prorationLines.Add((reference, line));
                continue;
            }

            var item = new InvoicePreviewItem
            {
                Reference = reference,
                Quantity = line.Quantity ?? 0,
                Cost = (price?.UnitAmountDecimal ?? 0) / 100m,
                Discounts = discounts.ItemLevel.GetValueOrDefault(reference),
            };
            if (!lineItemsByReference.TryAdd(reference, item))
            {
                // Preview invoices have no ID; the colliding price IDs are what locates the misconfiguration.
                throw DuplicateReference("The preview invoice", reference, invoice.Lines!.Data
                    .Where(invoiceLine => invoiceLine.Parent?.SubscriptionItemDetails?.Proration != true)
                    .Select(invoiceLine => invoiceLine.Pricing?.PriceDetails?.Price));
            }
        }

        return new InvoicePreview
        {
            PlanTier = planTier,
            Cadence = CadenceFromInvoice(invoice) ?? cadence,
            PasswordManager = BuildPasswordManagerItems(lineItemsByReference,
                SummarizeProrations(prorationLines, ProductType.PasswordManager)),
            SecretsManager = BuildSecretsManagerItems(lineItemsByReference,
                SummarizeProrations(prorationLines, ProductType.SecretsManager)),
            PrivilegedControls = BuildPrivilegedControlsItems(lineItemsByReference,
                SummarizeProrations(prorationLines, ProductType.PrivilegedControls)),
            Discounts = discounts.CartLevel.Length > 0 ? discounts.CartLevel : null,
            EstimatedTax = (invoice.TotalTaxes?.Sum(tax => tax.Amount) ?? 0) / 100m,
            Total = invoice.Total / 100m,
            AmountDue = invoice.AmountDue / 100m,
            StartingBalance = invoice.StartingBalance < 0 ? invoice.StartingBalance / 100m : null,
            NextPaymentAttempt = invoice.NextPaymentAttempt ?? invoice.DueDate,
        };
    }

    // The Password Manager seat's interval defines the plan cadence (add-ons can't override it); null when it's
    // absent (an all-proration invoice), leaving the caller's plan cadence as the fallback.
    private static PlanCadenceType? CadenceFromInvoice(Invoice invoice)
    {
        var seatInterval = invoice.Lines?.Data?
            .Where(line => line.Parent?.SubscriptionItemDetails?.Proration != true)
            .FirstOrDefault(line =>
                line.Pricing?.PriceDetails?.Price?.Metadata?.GetValueOrDefault(StripeConstants.MetadataKeys.PurchasableReference)
                    == StripeConstants.PurchasableReferences.PasswordManagerSeat)
            ?.Pricing?.PriceDetails?.Price?.Recurring?.Interval;

        return seatInterval switch
        {
            StripeConstants.Intervals.Year => PlanCadenceType.Annually,
            StripeConstants.Intervals.Month => PlanCadenceType.Monthly,
            _ => null
        };
    }

    /// <summary>Projects a subscription's current items into an <see cref="InvoicePreview"/>, without prorations, discounts, or tax.</summary>
    internal InvoicePreview Build(Subscription subscription, PlanTierType planTier, PlanCadenceType cadence)
    {
        var lineItemsByReference = new Dictionary<string, InvoicePreviewItem>();
        var total = 0m;

        foreach (var subscriptionItem in subscription.Items?.Data ?? [])
        {
            var unitCost = (subscriptionItem.Price?.UnitAmountDecimal ?? 0) / 100m;
            // Every item counts toward the total, even one we cannot place, so the total is never understated.
            total += subscriptionItem.Quantity * unitCost;

            var reference = ResolvePurchasableReference(subscriptionItem.Price);
            if (reference is null)
            {
                continue;
            }

            var item = new InvoicePreviewItem
            {
                Reference = reference,
                Quantity = subscriptionItem.Quantity,
                Cost = unitCost,
            };
            if (!lineItemsByReference.TryAdd(reference, item))
            {
                throw DuplicateReference($"Subscription {subscription.Id}", reference,
                    subscription.Items!.Data.Select(existingItem => existingItem.Price));
            }
        }

        // Password Manager seats are the projection's invariant; a missing line is a Stripe misconfiguration.
        // The invoice path doesn't require them: a preview invoice can legitimately be all prorations.
        if (!lineItemsByReference.ContainsKey(StripeConstants.PurchasableReferences.PasswordManagerSeat))
        {
            throw new InvalidOperationException("The preview resolved no Password Manager seats line.");
        }

        return new InvoicePreview
        {
            PlanTier = planTier,
            Cadence = cadence,
            PasswordManager = BuildPasswordManagerItems(lineItemsByReference, null),
            SecretsManager = BuildSecretsManagerItems(lineItemsByReference, null),
            PrivilegedControls = BuildPrivilegedControlsItems(lineItemsByReference, null),
            Discounts = null,
            EstimatedTax = 0m,
            Total = total,
            AmountDue = total,
            StartingBalance = null,
            NextPaymentAttempt = null,
        };
    }

    private string? ResolvePurchasableReference(Price? price)
    {
        var reference = price?.Metadata?.GetValueOrDefault(StripeConstants.MetadataKeys.PurchasableReference);
        if (string.IsNullOrEmpty(reference))
        {
            logger.LogError("Line has no purchasable reference; skipped. Price={PriceId}", price?.Id ?? "unknown");
            return null;
        }
        if (!PurchasableReferences.IsKnown(reference))
        {
            logger.LogError("Unknown purchasable reference {Reference} on price {PriceId}; skipped.", reference, price?.Id ?? "unknown");
            return null;
        }
        return reference;
    }

    // Two prices sharing a reference is a Stripe misconfiguration the preview refuses to guess at. Only built when
    // it's about to be thrown, so the hot path carries no bookkeeping for it.
    private static InvalidOperationException DuplicateReference(string subject, string reference, IEnumerable<Price?> prices)
    {
        var priceIds = prices
            .OfType<Price>()
            .Where(price => price.Metadata?.GetValueOrDefault(StripeConstants.MetadataKeys.PurchasableReference) == reference)
            .Select(price => price.Id);
        return new InvalidOperationException(
            $"{subject} resolved purchasable reference '{reference}' on more than one price: {string.Join(", ", priceIds)}.");
    }

    // One proration row per purchasable, so the client can tell which item each row offsets.
    private static PurchasableProration[]? SummarizeProrations(
        List<(string Reference, InvoiceLineItem Line)> prorationLines, ProductType product)
    {
        var rows = prorationLines
            .Where(proration => PurchasableReferences.ProductOf(proration.Reference) == product)
            .GroupBy(proration => proration.Reference)
            .Select(group => ProrationMapper.Summarize(group.Key, group.Select(proration => proration.Line).ToList()))
            .OfType<PurchasableProration>()
            .ToArray();
        return rows.Length > 0 ? rows : null;
    }

    private static PasswordManagerInvoiceItems BuildPasswordManagerItems(
        Dictionary<string, InvoicePreviewItem> lineItemsByReference, PurchasableProration[]? prorations)
    {
        var seats = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.PasswordManagerSeat);
        return new PasswordManagerInvoiceItems
        {
            Seats = seats,
            AdditionalStorage = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.PasswordManagerStorage),
            Prorations = prorations is { Length: > 0 } ? prorations : null,
        };
    }

    private static SecretsManagerInvoiceItems? BuildSecretsManagerItems(
        Dictionary<string, InvoicePreviewItem> lineItemsByReference, PurchasableProration[]? prorations)
    {
        var seats = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.SecretsManagerSeat);
        var serviceAccounts = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.SecretsManagerServiceAccount);
        // Keep the section whenever any line or proration resolved, so no resolved line drops out of the total.
        if (seats is null && serviceAccounts is null && prorations is not { Length: > 0 })
        {
            return null;
        }
        return new SecretsManagerInvoiceItems
        {
            Seats = seats,
            AdditionalServiceAccounts = serviceAccounts,
            Prorations = prorations is { Length: > 0 } ? prorations : null,
        };
    }

    private static PrivilegedControlsInvoiceItems? BuildPrivilegedControlsItems(
        Dictionary<string, InvoicePreviewItem> lineItemsByReference, PurchasableProration[]? prorations)
    {
        var seats = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.PrivilegedControlsSeat);
        if (seats is null && prorations is not { Length: > 0 })
        {
            return null;
        }
        return new PrivilegedControlsInvoiceItems
        {
            Seats = seats,
            Prorations = prorations is { Length: > 0 } ? prorations : null,
        };
    }
}
