using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;

namespace Bit.Invoicing.InvoicePreviews;

/// <summary>Reduces one product's proration lines into a single renderable credit row.</summary>
internal static class ProrationMapper
{
    internal static PurchasableProration? Summarize(IReadOnlyList<InvoiceLineItem> lines)
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
            Months = MonthsRemaining(lines),
        };
    }

    private static int MonthsRemaining(IReadOnlyList<InvoiceLineItem> lines)
    {
        var period = lines.Select(line => line.Period).FirstOrDefault(p => p is not null);
        if (period is null)
        {
            return 0;
        }

        // Calculate from the proration line's own span (End - Start); it is correct under any proration_behavior. (See README)
        var days = (period.End - period.Start).TotalDays;
        return Math.Max(1, (int)Math.Round(days / 30, MidpointRounding.AwayFromZero));
    }
}
