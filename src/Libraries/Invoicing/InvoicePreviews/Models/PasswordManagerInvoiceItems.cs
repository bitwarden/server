namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>Password Manager line items. Seats is required; a preview without it is invalid.</summary>
public record PasswordManagerInvoiceItems
{
    public required InvoicePreviewItem Seats { get; init; }
    public InvoicePreviewItem? AdditionalStorage { get; init; }
    public PurchasableProration[]? Prorations { get; init; }
}
