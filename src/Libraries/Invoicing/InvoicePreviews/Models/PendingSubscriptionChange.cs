namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>A scheduled future-phase price (e.g. an annual switch at renewal) and when it takes effect.</summary>
public record PendingSubscriptionChange
{
    public required InvoicePreview InvoicePreview { get; init; }
    public required DateTime EffectiveDate { get; init; }
}
