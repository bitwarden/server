namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>Password Manager line items. Present on every preview and always carries a seats line, at least one proration, or both.</summary>
public record PasswordManagerInvoiceItems
{
    public PasswordManagerInvoiceItems(
        InvoicePreviewItem? seats = null,
        InvoicePreviewItem? additionalStorage = null,
        PurchasableProration[]? prorations = null)
    {
        if (seats is null && prorations is not { Length: > 0 })
        {
            throw new ArgumentException("Password Manager items need a seats line or at least one proration.");
        }

        Seats = seats;
        AdditionalStorage = additionalStorage;
        Prorations = prorations;
    }

    /// <summary>Null when the invoice carries only Password Manager prorations, such as a Premium to organization upgrade previewed under always_invoice.</summary>
    public InvoicePreviewItem? Seats { get; init; }
    public InvoicePreviewItem? AdditionalStorage { get; init; }
    public PurchasableProration[]? Prorations { get; init; }
}
