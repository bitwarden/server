namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>One resolved line of the preview. Carries the purchasable reference, not a translation key; the client maps reference + plan tier + flow context to a key.</summary>
public record InvoicePreviewItem
{
    /// <summary>One of the values in StripeConstants.PurchasableReferences.</summary>
    public required string Reference { get; init; }

    public required long Quantity { get; init; }

    /// <summary>Unit cost in dollars.</summary>
    public required decimal Cost { get; init; }

    /// <summary>Item-scoped discounts (coupons with a non-empty applies-to set).</summary>
    public InvoicePreviewDiscount[]? Discounts { get; init; }
}
