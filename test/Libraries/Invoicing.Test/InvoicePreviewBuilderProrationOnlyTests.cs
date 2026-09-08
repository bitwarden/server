using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

/// <summary>Covers proration-only previews (<c>always_invoice</c>), where no non-proration seat line exists.</summary>
public class InvoicePreviewBuilderProrationOnlyTests
{
    private static InvoicePreviewBuilder Builder() => new(new RecordingLogger<InvoicePreviewBuilder>());

    [Fact]
    public void BuildFromInvoice_ProrationOnlyPmLines_SynthesizesSeatFromProrationCharge()
    {
        var invoice = StripeFixtures.SampleUpgradePreviewInvoiceProrationOnly();

        var preview = Builder().Build(invoice, PlanTierType.Families, PlanCadenceType.Annually);

        Assert.Equal("pm-seat", preview.PasswordManager.Seats.Reference);
        Assert.Equal(1, preview.PasswordManager.Seats.Quantity);
        Assert.Equal(26.67m, preview.PasswordManager.Seats.Cost);
        Assert.Null(preview.PasswordManager.AdditionalStorage);

        var proration = Assert.Single(preview.PasswordManager.Prorations!);
        Assert.Equal(6.67m, proration.Credit);
        Assert.Equal(26.67m, proration.Charge);
        Assert.Equal(20.00m, proration.Total);

        Assert.Equal(2.00m, preview.EstimatedTax);
        Assert.Equal(22.00m, preview.Total);
        Assert.Equal(22.00m, preview.AmountDue);
        Assert.Null(preview.SecretsManager);
    }

    [Fact]
    public void BuildFromInvoice_ProrationOnlyPmLines_SetsMonthsFromProrationSpan()
    {
        // Proration lines spanning ~8 months of remaining term, matching a 4-months-in annual upgrade.
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

        var proration = Assert.Single(preview.PasswordManager.Prorations!);
        Assert.Equal(8, proration.Months);
        Assert.Equal(26.67m, preview.PasswordManager.Seats.Cost);
    }

    [Fact]
    public void BuildFromInvoice_NoPmLinesAtAll_StillThrows()
    {
        // No purchasable_reference metadata anywhere: a genuine Stripe misconfiguration, not a proration-only preview.
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_no_pm", "total": 0, "amount_due": 0,
          "lines": { "data": [
            { "amount": 500, "quantity": 1, "pricing": { "price_details": { "price": { "id": "price_unlabeled" } } } }
          ] }
        }
        """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => Builder().Build(invoice, PlanTierType.Families, PlanCadenceType.Annually));
        Assert.Contains("no Password Manager seats line", exception.Message);
    }

    /// <summary>
    /// A skipped (unlabeled) line on a proration-only preview must not be synthesized into a seat, or the cart
    /// would not reconcile to the total. This is the partial-metadata state during the TSD-3229 rollout.
    /// </summary>
    [Fact]
    public void BuildFromInvoice_ProrationOnlyWithUnlabeledLine_Throws()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_partial_metadata", "total": 2200, "amount_due": 2200,
          "lines": { "data": [
            { "amount": -667, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_premium_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 2667, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_families_unlabeled" } } } }
          ] }
        }
        """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => Builder().Build(invoice, PlanTierType.Families, PlanCadenceType.Annually));
        Assert.Contains("skipped lines: 1", exception.Message);
    }

    [Fact]
    public void BuildFromInvoice_ProrationOnlyWithNoCharge_Throws()
    {
        // A credit with nothing charged is not an upgrade preview; refuse to synthesize a $0 seat from it.
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_credit_only", "total": -667, "amount_due": 0,
          "lines": { "data": [
            { "amount": -667, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_premium_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } }
          ] }
        }
        """);

        var exception = Assert.Throws<InvalidOperationException>(
            () => Builder().Build(invoice, PlanTierType.Families, PlanCadenceType.Annually));
        Assert.Contains("proration charge: 0", exception.Message);
    }

    /// <summary>When a real seat line is present, its unit amount wins over the proration charge.</summary>
    [Fact]
    public void BuildFromInvoice_RealSeatLineAndProrations_PrefersRealSeatLine()
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

        // 19.80 from the real line's unit_amount_decimal, not 37.97 from the proration charge.
        Assert.Equal(19.80m, preview.PasswordManager.Seats.Cost);
        Assert.Equal(2, preview.PasswordManager.Seats.Quantity);
        Assert.Equal(37.97m, Assert.Single(preview.PasswordManager.Prorations!).Charge);
    }
}
