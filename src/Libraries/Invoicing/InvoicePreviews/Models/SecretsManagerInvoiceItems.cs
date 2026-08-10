namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>Secrets Manager positions. Present only when the subscription carries Secrets Manager.</summary>
public record SecretsManagerInvoiceItems
{
    public required InvoicePreviewItem Seats { get; init; }
    public InvoicePreviewItem? AdditionalServiceAccounts { get; init; }
    public PurchasableProration[]? Prorations { get; init; }
}
