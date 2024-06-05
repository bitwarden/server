using Stripe;

namespace Bit.Api.Billing.Models.Responses;

public record InvoicesResponse(
    List<InvoiceDTO> Invoices)
{
    public static InvoicesResponse From(IEnumerable<Invoice> invoices) => new (
        invoices
            .Where(i => i.Status is "open" or "paid" or "uncollectible")
            .OrderByDescending(i => i.Created)
            .Select(InvoiceDTO.From).ToList());
}

public record InvoiceDTO(
    DateTime Date,
    string Number,
    decimal Total,
    string Status,
    string Url,
    string PdfUrl)
{
    public static InvoiceDTO From(Invoice invoice) => new (
        invoice.Created,
        invoice.Number,
        invoice.Total / 100M,
        invoice.Status,
        invoice.HostedInvoiceUrl,
        invoice.InvoicePdf);
}
