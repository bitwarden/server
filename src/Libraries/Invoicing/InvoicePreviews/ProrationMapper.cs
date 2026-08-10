using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews;

/// <summary>Reduces one product's proration lines into a single renderable credit row. Pure.</summary>
internal static class ProrationMapper
{
    internal static PurchasableProration? Summarize(IReadOnlyList<InvoiceLineItem> lines, Invoice invoice)
    {
        if (lines.Count == 0)
        {
            return null;
        }

        var chargeCents = lines.Where(line => line.Amount > 0).Sum(line => line.Amount);
        var creditCents = Math.Abs(lines.Where(line => line.Amount < 0).Sum(line => line.Amount));
        var netCents = chargeCents - creditCents;

        return new PurchasableProration
        {
            Charge = chargeCents / 100m,
            Credit = creditCents / 100m,
            Total = netCents / 100m,
            Tax = AllocateTax(netCents, invoice),
            Months = MonthsRemaining(lines, invoice),
        };
    }

    private static decimal AllocateTax(long netCents, Invoice invoice)
    {
        var totalTaxCents = invoice.TotalTaxes?.Sum(tax => tax.Amount) ?? 0;
        return totalTaxCents == 0 || invoice.Total == 0
            ? 0m
            : totalTaxCents * (netCents / (decimal)invoice.Total) / 100m;
    }

    private static int MonthsRemaining(IReadOnlyList<InvoiceLineItem> lines, Invoice invoice)
    {
        var lineEnd = lines.Select(line => line.Period?.End).FirstOrDefault(end => end.HasValue);
        if (lineEnd is null)
        {
            return 0;
        }

        var months = ((invoice.PeriodEnd.Year - lineEnd.Value.Year) * 12) + invoice.PeriodEnd.Month - lineEnd.Value.Month;
        return Math.Max(0, months);
    }
}
