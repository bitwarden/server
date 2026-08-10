namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>Password Manager positions. Seats is required; a preview without it is invalid.</summary>
public record PasswordManagerInvoiceItems
{
    public required InvoicePreviewItem Seats { get; init; }
    public InvoicePreviewItem? AdditionalStorage { get; init; }
    public PurchasableProration[]? Prorations { get; init; }
}
