namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>Password Manager line items. Seats is null only when the invoice is entirely prorations.</summary>
public record PasswordManagerInvoiceItems
{
    public InvoicePreviewItem? Seats { get; init; }
    public InvoicePreviewItem? AdditionalStorage { get; init; }
    public PurchasableProration[]? Prorations { get; init; }
}
