using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class InvoicePreviewBuilderProrationMonthsTests
{
    private static InvoicePreviewBuilder Builder() => new(new RecordingLogger<InvoicePreviewBuilder>());

    // Captured live from Stripe (test mode): an annual subscription (premium-annually-2026) on a test
    // clock advanced 15 days into the term, previewed with proration_behavior=create_prorations.
    // The proration line spans [now, period_end] = [1788901346, 1819141346] (~350 days), while the
    // invoice's period_end is ALSO 1819141346 — so the old formula (line.End - invoice.PeriodEnd) gave
    // 0 days -> Months = 1, when the true remaining term is 12 months. This fixture pins that real shape
    // so the collapse can never come back. See the library README's Stripe boundary note.
    [Fact]
    public void BuildFromInvoice_AnnualProrationUnderCreateProrations_RendersTrueMonthsNotOne()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_annual_proration_months", "total": 5858, "amount_due": 5858, "period_end": 1819141346,
          "lines": { "data": [
            { "amount": 3797, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1788901346, "end": 1819141346 } },
            { "amount": -1899, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1788901346, "end": 1819141346 } },
            { "amount": 3960, "quantity": 2,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "unit_amount_decimal": "1980", "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1819141346, "end": 1850677346 } }
          ] }
        }
        """);

        var preview = Builder().Build(invoice, PlanTierType.Premium, PlanCadenceType.Annually);

        var proration = Assert.Single(preview.PasswordManager.Prorations!);
        Assert.Equal(12, proration.Months);
        Assert.Equal(37.97m, proration.Charge);
        Assert.Equal(18.99m, proration.Credit);
        Assert.Equal(18.98m, proration.Total);
    }
}
