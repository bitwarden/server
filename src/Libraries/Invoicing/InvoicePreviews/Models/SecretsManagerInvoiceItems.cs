namespace Bit.Invoicing.InvoicePreviews.Models;

/// <summary>Secrets Manager line items. Present when the subscription carries Secrets Manager or the invoice includes a Secrets Manager proration.</summary>
public record SecretsManagerInvoiceItems
{
    /// <summary>Null when the invoice carries only a Secrets Manager proration, such as a mid-cycle removal.</summary>
    public InvoicePreviewItem? Seats { get; init; }
    public InvoicePreviewItem? AdditionalServiceAccounts { get; init; }
    public PurchasableProration[]? Prorations { get; init; }
}
