using Stripe;

namespace Bit.Invoicing.InvoicePreviews.Stripe;

internal interface IInvoicePreviewClient
{
    Task<Invoice> GetInvoiceForPreviewAsync(InvoiceCreatePreviewOptions options);
}
