using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class InvoicePreviewBuilderPrivilegedControlsTests
{
    private static InvoicePreviewBuilder Builder(out RecordingLogger<InvoicePreviewBuilder> logger)
    {
        logger = new RecordingLogger<InvoicePreviewBuilder>();
        return new InvoicePreviewBuilder(logger);
    }

    [Fact]
    public void BuildFromInvoice_PamSeatLine_RendersPrivilegedControlsSection()
    {
        var invoice = StripeFixtures.SampleInvoiceWithPmAndPamSeat();

        var preview = Builder(out var logger).Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Monthly);

        Assert.NotNull(preview.PrivilegedControls);
        var seats = preview.PrivilegedControls!.Seats;
        Assert.NotNull(seats);
        Assert.Equal("pam-seat", seats!.Reference);
        Assert.Equal(5, seats.Quantity);
        Assert.Equal(12.00m, seats.Cost);
        Assert.Null(preview.PrivilegedControls.Prorations);

        // Password Manager is unaffected, and the cadence stays anchored on the pm-seat line.
        Assert.Equal("pm-seat", preview.PasswordManager.Seats!.Reference);
        Assert.Equal(72.00m, preview.PasswordManager.Seats.Cost);
        Assert.Null(preview.PasswordManager.Prorations);
        Assert.Null(preview.SecretsManager);
        Assert.Equal(PlanCadenceType.Annually, preview.Cadence);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void BuildFromInvoice_PamSeatLine_ReconcilesToTotal()
    {
        var invoice = StripeFixtures.SampleInvoiceWithPmAndPamSeat();

        var preview = Builder(out _).Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        var pmSeats = preview.PasswordManager.Seats!;
        var pcSeats = preview.PrivilegedControls!.Seats!;
        Assert.Equal(preview.Total, pmSeats.Quantity * pmSeats.Cost + pcSeats.Quantity * pcSeats.Cost);
    }

    [Fact]
    public void BuildFromInvoice_PamSeatProration_GroupsUnderPrivilegedControls()
    {
        var invoice = StripeFixtures.SampleInvoiceWithPmAndPamSeatProration();

        var preview = Builder(out _).Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        var section = preview.PrivilegedControls;
        Assert.NotNull(section);
        Assert.Equal(10, section!.Seats!.Quantity);
        var proration = Assert.Single(section.Prorations!);
        Assert.Equal("pam-seat", proration.Reference);
        Assert.Equal(110.14m, proration.Charge);
        Assert.Equal(55.07m, proration.Credit);
        Assert.Equal(55.07m, proration.Total);
        Assert.Equal(11, proration.Months);

        Assert.Null(preview.PasswordManager.Prorations);
        Assert.Null(preview.SecretsManager);

        var pmSeats = preview.PasswordManager.Seats!;
        Assert.Equal(
            preview.Total,
            pmSeats.Quantity * pmSeats.Cost + section.Seats.Quantity * section.Seats.Cost + proration.Total);
    }

    // A mid-cycle Privileged Controls removal: a pam-seat proration credit with no recurring pam-seat line.
    [Fact]
    public void BuildFromInvoice_PamSeatRemovedMidCycle_ProrationOnlySection()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_pam_removal", "total": 30493, "amount_due": 30493, "period_end": 1821305920,
          "lines": { "data": [
            { "amount": -5507, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pam_seat_annually", "metadata": { "purchasable_reference": "pam-seat" } } } },
              "period": { "start": 1792361920, "end": 1821305920 } },
            { "amount": 36000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat_annually", "unit_amount_decimal": "7200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1821305920, "end": 1852841920 } }
          ] }
        }
        """);

        var preview = Builder(out _).Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.NotNull(preview.PrivilegedControls);
        Assert.Null(preview.PrivilegedControls!.Seats);
        var proration = Assert.Single(preview.PrivilegedControls.Prorations!);
        Assert.Equal(55.07m, proration.Credit);
        Assert.Equal(-55.07m, proration.Total);

        var pmSeats = preview.PasswordManager.Seats!;
        Assert.Equal(304.93m, preview.Total);
        Assert.Equal(preview.Total, pmSeats.Quantity * pmSeats.Cost + proration.Total);
    }

    [Fact]
    public void BuildFromInvoice_MixedProrations_RoutesEachToItsSection()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_mixed_prorations", "total": 40600, "amount_due": 40600, "period_end": 1821305920,
          "lines": { "data": [
            { "amount": 2000, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat_annually", "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1792361920, "end": 1821305920 } },
            { "amount": 1500, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_sm_seat_annually", "metadata": { "purchasable_reference": "sm-seat" } } } },
              "period": { "start": 1792361920, "end": 1821305920 } },
            { "amount": 1100, "quantity": 1,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pam_seat_annually", "metadata": { "purchasable_reference": "pam-seat" } } } },
              "period": { "start": 1792361920, "end": 1821305920 } },
            { "amount": 36000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat_annually", "unit_amount_decimal": "7200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1821305920, "end": 1852841920 } }
          ] }
        }
        """);

        var preview = Builder(out _).Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        var pmProration = Assert.Single(preview.PasswordManager.Prorations!);
        Assert.Equal("pm-seat", pmProration.Reference);
        var smProration = Assert.Single(preview.SecretsManager!.Prorations!);
        Assert.Equal("sm-seat", smProration.Reference);
        var pcProration = Assert.Single(preview.PrivilegedControls!.Prorations!);
        Assert.Equal("pam-seat", pcProration.Reference);
        Assert.Equal(11.00m, pcProration.Charge);

        var pmSeats = preview.PasswordManager.Seats!;
        Assert.Equal(
            preview.Total,
            pmSeats.Quantity * pmSeats.Cost + pmProration.Total + smProration.Total + pcProration.Total);
    }

    [Fact]
    public void BuildFromInvoice_NoPamActivity_LeavesSectionNull()
    {
        var invoice = StripeFixtures.SampleInvoiceWithPmSeat();

        var preview = Builder(out _).Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Null(preview.PrivilegedControls);
    }

    // Two prices sharing the pam-seat reference (e.g. old and new price IDs during a price migration) is a Stripe
    // misconfiguration the preview refuses to guess at. The preview invoice has no ID, so the error names the
    // colliding prices; neither test price ID is a substring of the other, so each assertion can fail on its own.
    [Fact]
    public void BuildFromInvoice_DuplicatePamSeat_ThrowsNamingBothPrices()
    {
        var invoice = Invoice.FromJson("""
        {
          "total": 48500, "amount_due": 48500,
          "lines": { "data": [
            { "amount": 36000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat_annually", "unit_amount_decimal": "7200", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 6000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pam_seat_old", "unit_amount_decimal": "1200", "metadata": { "purchasable_reference": "pam-seat" } } } } },
            { "amount": 6500, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pam_seat_new", "unit_amount_decimal": "1300", "metadata": { "purchasable_reference": "pam-seat" } } } } }
          ] }
        }
        """);

        var builder = Builder(out _);

        var exception = Assert.Throws<InvalidOperationException>(
            () => builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually));
        Assert.Contains("price_pam_seat_old", exception.Message);
        Assert.Contains("price_pam_seat_new", exception.Message);
    }

    [Fact]
    public void BuildFromSubscription_DuplicatePamSeat_ThrowsNamingBothPrices()
    {
        var subscription = Subscription.FromJson("""
        {
          "id": "sub_preview_duplicate_pam",
          "items": { "data": [
            { "id": "si_pm", "quantity": 5, "price": { "id": "price_pm_seat_annually", "unit_amount": 7200, "unit_amount_decimal": "7200", "metadata": { "purchasable_reference": "pm-seat" } } },
            { "id": "si_pam_1", "quantity": 5, "price": { "id": "price_pam_seat_old", "unit_amount": 1200, "unit_amount_decimal": "1200", "metadata": { "purchasable_reference": "pam-seat" } } },
            { "id": "si_pam_2", "quantity": 5, "price": { "id": "price_pam_seat_new", "unit_amount": 1300, "unit_amount_decimal": "1300", "metadata": { "purchasable_reference": "pam-seat" } } }
          ] }
        }
        """);

        var builder = Builder(out _);

        var exception = Assert.Throws<InvalidOperationException>(
            () => builder.Build(subscription, PlanTierType.Enterprise, PlanCadenceType.Annually));
        Assert.Contains("sub_preview_duplicate_pam", exception.Message);
        Assert.Contains("price_pam_seat_old", exception.Message);
        Assert.Contains("price_pam_seat_new", exception.Message);
    }

    // Registering pam-seat as known lets a coupon scoped to the Privileged Controls product attach to its item,
    // where before it logged "matched no line" and was dropped.
    [Fact]
    public void BuildFromInvoice_ItemScopedDiscountOnPamSeat_AttachesToPamItem()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_pam_discount", "total": 39000, "amount_due": 39000,
          "total_discount_amounts": [
            { "amount": 3000, "discount": { "id": "di_pam", "source": { "coupon": { "id": "cp_pam", "name": "PAM50", "percent_off": 50, "applies_to": { "products": ["prod_pam"] } } } } }
          ],
          "lines": { "data": [
            { "amount": 36000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat_annually", "unit_amount_decimal": "7200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 6000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pam_seat_annually", "unit_amount_decimal": "1200", "recurring": { "interval": "year" }, "metadata": { "purchasable_reference": "pam-seat" } } } },
              "discount_amounts": [ { "amount": 3000, "discount": "di_pam" } ] }
          ] }
        }
        """);

        var preview = Builder(out var logger).Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        var discount = Assert.Single(preview.PrivilegedControls!.Seats!.Discounts!);
        Assert.Equal(BitwardenDiscountType.PercentOff, discount.Type);
        Assert.Equal(50m, discount.Value);
        Assert.Equal(30.00m, discount.Amount);
        Assert.Equal("PAM50", discount.Label);
        Assert.Null(preview.PasswordManager.Seats!.Discounts);
        Assert.Null(preview.Discounts);
        Assert.DoesNotContain(logger.Errors, e => e.Contains("matched no line"));

        // 5 × 72.00 + 5 × 12.00 − 30.00: the discount is reported per line, so the rows still reconcile.
        var pmSeats = preview.PasswordManager.Seats;
        var pcSeats = preview.PrivilegedControls.Seats;
        Assert.Equal(390.00m, preview.Total);
        Assert.Equal(preview.Total, pmSeats.Quantity * pmSeats.Cost + pcSeats.Quantity * pcSeats.Cost - discount.Amount);
    }

    // A first Privileged Controls purchase mid-cycle, previewed with always_invoice: a single pam-seat proration
    // charge and no recurring lines at all, so neither seat section has a line and the cadence falls back to the caller.
    [Fact]
    public void BuildFromInvoice_PamSeatFirstPurchaseMidCycle_ProrationOnlyInvoice()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_pam_first_purchase", "total": 11014, "amount_due": 11014, "period_end": 1821305920,
          "lines": { "data": [
            { "amount": 11014, "quantity": 10,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pam_seat_annually", "metadata": { "purchasable_reference": "pam-seat" } } } },
              "period": { "start": 1792361920, "end": 1821305920 } }
          ] }
        }
        """);

        var preview = Builder(out var logger).Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Monthly);

        Assert.Null(preview.PasswordManager.Seats);
        Assert.Null(preview.PasswordManager.Prorations);
        Assert.Null(preview.SecretsManager);
        Assert.NotNull(preview.PrivilegedControls);
        Assert.Null(preview.PrivilegedControls!.Seats);
        var proration = Assert.Single(preview.PrivilegedControls.Prorations!);
        Assert.Equal(0m, proration.Credit);
        Assert.Equal(110.14m, proration.Charge);
        Assert.Equal(110.14m, proration.Total);
        Assert.Equal(PlanCadenceType.Monthly, preview.Cadence);
        Assert.Equal(preview.Total, proration.Total);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void BuildFromSubscription_PamSeatItem_PlacedAndCountedOnce()
    {
        var subscription = StripeFixtures.SampleSubscriptionWithPmAndPamSeat();

        var preview = Builder(out var logger).Build(subscription, PlanTierType.Enterprise, PlanCadenceType.Annually);

        var section = preview.PrivilegedControls;
        Assert.NotNull(section);
        Assert.Equal("pam-seat", section!.Seats!.Reference);
        Assert.Equal(5, section.Seats.Quantity);
        Assert.Equal(12.00m, section.Seats.Cost);
        Assert.Null(section.Prorations);

        // 5 × 72.00 + 5 × 12.00: the Privileged Controls item is counted once, and the visible rows explain the total.
        var pmSeats = preview.PasswordManager.Seats!;
        Assert.Equal(420.00m, preview.Total);
        Assert.Equal(preview.Total, preview.AmountDue);
        Assert.Equal(preview.Total, pmSeats.Quantity * pmSeats.Cost + section.Seats.Quantity * section.Seats.Cost);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void BuildFromSubscription_NoPamItem_LeavesSectionNull()
    {
        var subscription = StripeFixtures.SampleSubscriptionWithPmSeat();

        var preview = Builder(out _).Build(subscription, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Null(preview.PrivilegedControls);
    }
}
