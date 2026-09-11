using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class InvoicePreviewBuilderProrationOnlyTests
{
    private static InvoicePreviewBuilder Builder() => new(new RecordingLogger<InvoicePreviewBuilder>());

    [Fact]
    public void BuildFromInvoice_ProrationOnlyPmLines_LeavesSeatsNullAndCarriesTheProration()
    {
        var invoice = StripeFixtures.SampleUpgradePreviewInvoiceProrationOnly();

        var preview = Builder().Build(invoice, PlanTierType.Families, PlanCadenceType.Annually);

        Assert.Null(preview.PasswordManager.Seats);
        Assert.Null(preview.PasswordManager.AdditionalStorage);

        var proration = Assert.Single(preview.PasswordManager.Prorations!);
        Assert.Equal(26.67m, proration.Charge);
        Assert.Equal(6.67m, proration.Credit);
        Assert.Equal(20.00m, proration.Total);

        Assert.Equal(2.00m, preview.EstimatedTax);
        Assert.Equal(22.00m, preview.Total);
        Assert.Equal(22.00m, preview.AmountDue);
        Assert.Null(preview.SecretsManager);
    }

    [Fact]
    public void BuildFromInvoice_ProrationOnlyPmLines_SetsMonthsFromProrationSpan()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_upgrade_months", "total": 2200, "amount_due": 2200,
          "lines": { "data": [
            { "amount": -667, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_premium_seat", "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1788901346, "end": 1809637346 } },
            { "amount": 2667, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_families", "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1788901346, "end": 1809637346 } }
          ] }
        }
        """);

        var preview = Builder().Build(invoice, PlanTierType.Families, PlanCadenceType.Annually);

        Assert.Null(preview.PasswordManager.Seats);
        Assert.Equal(8, Assert.Single(preview.PasswordManager.Prorations!).Months);
    }

    [Fact]
    public void BuildFromInvoice_NoPmSeatsLineAndNoPmProration_Throws()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_no_pm", "total": 500, "amount_due": 500,
          "lines": { "data": [
            { "amount": 500, "quantity": 1, "pricing": { "price_details": { "price": { "id": "price_unlabeled" } } } }
          ] }
        }
        """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => Builder().Build(invoice, PlanTierType.Families, PlanCadenceType.Annually));
        Assert.Contains("no Password Manager seats line", exception.Message);
    }

    [Fact]
    public void BuildFromInvoice_SeatsLineAndProrations_KeepsBoth()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_real_seat_and_prorations", "total": 5858, "amount_due": 5858,
          "lines": { "data": [
            { "amount": 3797, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": -1899, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 3960, "quantity": 2,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "unit_amount_decimal": "1980", "metadata": { "purchasable_reference": "pm-seat" } } } } }
          ] }
        }
        """);

        var preview = Builder().Build(invoice, PlanTierType.Teams, PlanCadenceType.Annually);

        Assert.Equal(19.80m, preview.PasswordManager.Seats!.Cost);
        Assert.Equal(2, preview.PasswordManager.Seats.Quantity);
        Assert.Equal(37.97m, Assert.Single(preview.PasswordManager.Prorations!).Charge);
    }
}
