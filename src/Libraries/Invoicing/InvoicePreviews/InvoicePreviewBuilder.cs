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
        var passwordManagerProrations = new List<InvoiceLineItem>();
        var secretsManagerProrations = new List<InvoiceLineItem>();
        var discounts = DiscountMapper.Partition(invoice, logger);

        foreach (var line in invoice.Lines?.Data ?? [])
        {
            var reference = ResolvePurchasableReference(line.Pricing?.PriceDetails?.Price);
            if (reference is null)
            {
                continue;
            }

            if (line.Parent?.SubscriptionItemDetails?.Proration == true)
            {
                switch (PurchasableReferences.ProductOf(reference))
                {
                    case ProductType.PasswordManager:
                        passwordManagerProrations.Add(line);
                        break;
                    case ProductType.SecretsManager:
                        secretsManagerProrations.Add(line);
                        break;
                    case null:
                        logger.LogError("Proration line references {Reference}, which has no product mapping; skipped.", reference);
                        break;
                }
                continue;
            }

            var item = new InvoicePreviewItem
            {
                Reference = reference,
                Quantity = line.Quantity ?? 0,
                Cost = line.Amount / 100m,
                Discounts = discounts.ItemLevel.GetValueOrDefault(reference),
            };
            if (!lineItemsByReference.TryAdd(reference, item))
            {
                logger.LogError("Duplicate purchasable reference {Reference} on invoice; kept the first line.", reference);
            }
        }

        return new InvoicePreview
        {
            PlanTier = planTier,
            Cadence = cadence,
            PasswordManager = BuildPasswordManagerItems(lineItemsByReference, ProrationMapper.Summarize(passwordManagerProrations, invoice)),
            SecretsManager = BuildSecretsManagerItems(lineItemsByReference, ProrationMapper.Summarize(secretsManagerProrations, invoice)),
            Discounts = discounts.CartLevel.Length > 0 ? discounts.CartLevel : null,
            EstimatedTax = (invoice.TotalTaxes?.Sum(tax => tax.Amount) ?? 0) / 100m,
            Total = invoice.Total / 100m,
            AmountDue = invoice.AmountDue / 100m,
            StartingBalance = invoice.StartingBalance < 0 ? invoice.StartingBalance / 100m : null,
            NextPaymentAttempt = null,
        };
    }

    /// <summary>Projects a subscription's current items into an <see cref="InvoicePreview"/>, without prorations, discounts, or tax.</summary>
    internal InvoicePreview Build(Subscription subscription, PlanTierType planTier, PlanCadenceType cadence)
    {
        var lineItemsByReference = new Dictionary<string, InvoicePreviewItem>();
        var total = 0m;

        foreach (var subscriptionItem in subscription.Items?.Data ?? [])
        {
            // Every item counts toward the total, even one we cannot place, so the total is never understated.
            var cost = subscriptionItem.Quantity * (subscriptionItem.Price?.UnitAmount ?? 0) / 100m;
            total += cost;

            var reference = ResolvePurchasableReference(subscriptionItem.Price);
            if (reference is null)
            {
                continue;
            }

            var item = new InvoicePreviewItem
            {
                Reference = reference,
                Quantity = subscriptionItem.Quantity,
                Cost = cost,
            };
            if (!lineItemsByReference.TryAdd(reference, item))
            {
                logger.LogError("Duplicate purchasable reference {Reference} on subscription; kept the first item.", reference);
            }
        }

        return new InvoicePreview
        {
            PlanTier = planTier,
            Cadence = cadence,
            PasswordManager = BuildPasswordManagerItems(lineItemsByReference, null),
            SecretsManager = BuildSecretsManagerItems(lineItemsByReference, null),
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

    private static PasswordManagerInvoiceItems BuildPasswordManagerItems(
        Dictionary<string, InvoicePreviewItem> lineItemsByReference, PurchasableProration? proration)
    {
        var seats = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.PasswordManagerSeat)
            ?? throw new InvalidOperationException("The preview resolved no Password Manager seats line.");
        return new PasswordManagerInvoiceItems
        {
            Seats = seats,
            AdditionalStorage = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.PasswordManagerStorage),
            Prorations = proration is { } p ? [p] : null,
        };
    }

    private static SecretsManagerInvoiceItems? BuildSecretsManagerItems(
        Dictionary<string, InvoicePreviewItem> lineItemsByReference, PurchasableProration? proration)
    {
        var seats = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.SecretsManagerSeat);
        if (seats is null)
        {
            return null;
        }
        return new SecretsManagerInvoiceItems
        {
            Seats = seats,
            AdditionalServiceAccounts = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.SecretsManagerServiceAccount),
            Prorations = proration is { } p ? [p] : null,
        };
    }
}
