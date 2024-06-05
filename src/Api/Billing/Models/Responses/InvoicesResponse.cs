using Stripe;

namespace Bit.Api.Billing.Models.Responses;

public record InvoicesResponse(
    List<InvoiceDTO> Invoices)
{
    public static InvoicesResponse From(IEnumerable<Invoice> invoices) => new (
        invoices
            .Where(i => i.Status is "open" or "paid" or "uncollectible")
            .Select(InvoiceDTO.From).ToList());
}

public record InvoiceDTO(
    DateTime Date,
    string Number,
    decimal Total,
    string Status)
{
    public static InvoiceDTO From(Invoice invoice) => new InvoiceDTO(
        invoice.Created,
        invoice.Number,
        invoice.Total,
        invoice.Status);
}
