namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>Password Manager line items. Present on every preview; a preview without a seats line or a proration is invalid.</summary>
public record PasswordManagerInvoiceItems
{
    /// <summary>Null when the invoice carries only Password Manager prorations, such as a Premium to organization upgrade previewed under always_invoice.</summary>
    public InvoicePreviewItem? Seats { get; init; }
    public InvoicePreviewItem? AdditionalStorage { get; init; }
    public PurchasableProration[]? Prorations { get; init; }
}
