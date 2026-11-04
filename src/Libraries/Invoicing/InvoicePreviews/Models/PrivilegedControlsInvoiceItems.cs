namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>Privileged Controls line items. Present when the subscription carries a Privileged Controls line or the invoice includes a Privileged Controls proration.</summary>
public record PrivilegedControlsInvoiceItems
{
    /// <summary>Null when the invoice carries only a Privileged Controls proration, such as a mid-cycle removal.</summary>
    public InvoicePreviewItem? Seats { get; init; }
    public PurchasableProration[]? Prorations { get; init; }
}
