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
            var price = line.Pricing?.PriceDetails?.Price;
            var reference = ResolvePurchasableReference(price);
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
                }
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
                throw new InvalidOperationException($"The preview resolved a duplicate purchasable reference '{reference}' on the invoice.");
            }
        }

        return new InvoicePreview
        {
            PlanTier = planTier,
            Cadence = cadence,
            PasswordManager = BuildPasswordManagerItems(lineItemsByReference, ProrationMapper.Summarize(passwordManagerProrations)),
            SecretsManager = BuildSecretsManagerItems(lineItemsByReference, ProrationMapper.Summarize(secretsManagerProrations)),
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
                throw new InvalidOperationException($"The preview resolved a duplicate purchasable reference '{reference}' on the subscription.");
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
        // Password Manager seats are always present; a missing line is a Stripe misconfiguration, unlike Secrets Manager.
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
        var serviceAccounts = lineItemsByReference.GetValueOrDefault(StripeConstants.PurchasableReferences.SecretsManagerServiceAccount);
        // Keep the section whenever any line or proration resolved, so no resolved line drops out of the total.
        if (seats is null && serviceAccounts is null && proration is null)
        {
            return null;
        }
        return new SecretsManagerInvoiceItems
        {
            Seats = seats,
            AdditionalServiceAccounts = serviceAccounts,
            Prorations = proration is { } p ? [p] : null,
        };
    }
}
