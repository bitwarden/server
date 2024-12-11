using Stripe;

namespace Bit.Api.Billing.Models.Responses;

public record InvoicesResponse(List<InvoiceResponse> Invoices)
{
    public static InvoicesResponse From(IEnumerable<Invoice> invoices) =>
        new(
            invoices
                .Where(i => i.Status is "open" or "paid" or "uncollectible")
                .OrderByDescending(i => i.Created)
                .Select(InvoiceResponse.From)
                .ToList()
        );
}

public record InvoiceResponse(
    string Id,
    DateTime Date,
    string Number,
    decimal Total,
    string Status,
    DateTime? DueDate,
    string Url
)
{
    public static InvoiceResponse From(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.Created,
            invoice.Number,
            invoice.Total / 100M,
            invoice.Status,
            invoice.DueDate,
            invoice.HostedInvoiceUrl
        );
}
