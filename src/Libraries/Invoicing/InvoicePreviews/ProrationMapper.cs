using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews;

/// <summary>Reduces one product's proration lines into a single renderable credit row.</summary>
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

        return new PurchasableProration
        {
            Charge = chargeCents / 100m,
            Credit = creditCents / 100m,
            Total = (chargeCents - creditCents) / 100m,
            Tax = lines.SelectMany(line => line.Taxes ?? []).Sum(tax => tax.Amount) / 100m,
            Months = MonthsRemaining(lines, invoice),
        };
    }

    private static int MonthsRemaining(IReadOnlyList<InvoiceLineItem> lines, Invoice invoice)
    {
        var lineEnd = lines.Select(line => line.Period?.End).FirstOrDefault(end => end.HasValue);
        if (lineEnd is null)
        {
            return 0;
        }

        // 30-day months, minimum one, matching the legacy proration display.
        var days = (lineEnd.Value - invoice.PeriodEnd).TotalDays;
        return Math.Max(1, (int)Math.Round(days / 30, MidpointRounding.AwayFromZero));
    }
}
