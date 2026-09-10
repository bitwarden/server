using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class InvoicePreviewBuilderTests
{
    private static InvoicePreviewBuilder Builder(out RecordingLogger<InvoicePreviewBuilder> logger)
    {
        logger = new RecordingLogger<InvoicePreviewBuilder>();
        return new InvoicePreviewBuilder(logger);
    }

    private static Invoice Deserialize(string json) => Invoice.FromJson(json);

    private static Subscription DeserializeSubscription(string json) => Subscription.FromJson(json);

    [Fact]
    public void BuildFromInvoice_MissingReference_LogsAndSkips()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 12790, "amount_due": 12790,
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm", "unit_amount_decimal": "2558", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 500, "quantity": 1, "pricing": { "price_details": { "price": { "id": "price_mystery", "metadata": {} } } } }
          ] }
        }
        """);

        var builder = Builder(out var logger);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Equal("pm-seat", preview.PasswordManager.Seats.Reference);
        Assert.Equal(25.58m, preview.PasswordManager.Seats.Cost);
        Assert.Contains(logger.Errors, e => e.Contains("price_mystery"));
    }

    [Fact]
    public void BuildFromInvoice_RoutesEachReferenceToItsPosition()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 22218, "amount_due": 22218,
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm_seat", "unit_amount_decimal": "2558", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 3582, "quantity": 2, "pricing": { "price_details": { "price": { "id": "price_pm_storage", "unit_amount_decimal": "1791", "metadata": { "purchasable_reference": "pm-storage" } } } } },
            { "amount": 4500, "quantity": 3, "pricing": { "price_details": { "price": { "id": "price_sm_seat", "unit_amount_decimal": "1500", "metadata": { "purchasable_reference": "sm-seat" } } } } },
            { "amount": 1279, "quantity": 1, "pricing": { "price_details": { "price": { "id": "price_sm_sa", "unit_amount_decimal": "1279", "metadata": { "purchasable_reference": "sm-service-account" } } } } }
          ] }
        }
        """);

        var builder = Builder(out _);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Equal(5, preview.PasswordManager.Seats.Quantity);
        Assert.Equal(25.58m, preview.PasswordManager.Seats.Cost);
        Assert.Equal(2, preview.PasswordManager.AdditionalStorage!.Quantity);
        Assert.Equal(17.91m, preview.PasswordManager.AdditionalStorage.Cost);
        Assert.Equal(3, preview.SecretsManager!.Seats.Quantity);
        Assert.Equal(15.00m, preview.SecretsManager.Seats.Cost);
        Assert.Equal(1, preview.SecretsManager.AdditionalServiceAccounts!.Quantity);
        Assert.Equal(12.79m, preview.SecretsManager.AdditionalServiceAccounts.Cost);
    }

    [Fact]
    public void BuildFromInvoice_MapsEnvelope()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 13997, "amount_due": 13997, "starting_balance": -1234,
          "total_taxes": [ { "amount": 812 }, { "amount": 395 } ],
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } }
          ] }
        }
        """);

        var builder = Builder(out _);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Equal(12.07m, preview.EstimatedTax);
        Assert.Equal(139.97m, preview.Total);
        Assert.Equal(139.97m, preview.AmountDue);
        Assert.Equal(-12.34m, preview.StartingBalance);
        Assert.Null(preview.NextPaymentAttempt);
    }

    [Fact]
    public void BuildFromInvoice_PositiveStartingBalance_IsNotCarried()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 12790, "amount_due": 12790, "starting_balance": 1234,
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } }
          ] }
        }
        """);

        var builder = Builder(out _);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Null(preview.StartingBalance);
    }

    [Fact]
    public void BuildFromInvoice_FoldsProrations_ByProductReference()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 17856, "amount_due": 17856,
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 4567, "quantity": 3, "pricing": { "price_details": { "price": { "id": "price_sm_seat", "metadata": { "purchasable_reference": "sm-seat" } } } } },
            { "amount": 2007, "pricing": { "price_details": { "price": { "id": "price_pm_prorate", "metadata": { "purchasable_reference": "pm-storage" } } } }, "parent": { "subscription_item_details": { "proration": true } } },
            { "amount": -1508, "pricing": { "price_details": { "price": { "id": "price_sm_prorate", "metadata": { "purchasable_reference": "sm-service-account" } } } }, "parent": { "subscription_item_details": { "proration": true } } }
          ] }
        }
        """);

        var builder = Builder(out _);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Null(preview.PasswordManager.AdditionalStorage);
        Assert.Null(preview.SecretsManager!.AdditionalServiceAccounts);

        var pmProration = Assert.Single(preview.PasswordManager.Prorations!);
        Assert.Equal(20.07m, pmProration.Charge);
        Assert.Equal(0m, pmProration.Credit);
        Assert.Equal(20.07m, pmProration.Total);

        var smProration = Assert.Single(preview.SecretsManager.Prorations!);
        Assert.Equal(0m, smProration.Charge);
        Assert.Equal(15.08m, smProration.Credit);
        Assert.Equal(-15.08m, smProration.Total);
    }

    [Fact]
    public void BuildFromInvoice_ReadsReferenceBeforeProrationCheck()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 13290, "amount_due": 13290,
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 500, "pricing": { "price_details": { "price": { "id": "price_orphan", "metadata": {} } } }, "parent": { "subscription_item_details": { "proration": true } } }
          ] }
        }
        """);

        var builder = Builder(out var logger);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Null(preview.PasswordManager.Prorations);
        Assert.Contains(logger.Errors, e => e.Contains("price_orphan"));
    }

    [Fact]
    public void BuildFromInvoice_UsesParentSubscriptionItemDetailsProration_NotTopLevel()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 17606, "amount_due": 17606,
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 3582, "quantity": 2, "proration": true, "pricing": { "price_details": { "price": { "id": "price_pm_storage_pos", "unit_amount_decimal": "1791", "metadata": { "purchasable_reference": "pm-storage" } } } }, "parent": { "subscription_item_details": { "proration": false } } },
            { "amount": 1234, "proration": false, "pricing": { "price_details": { "price": { "id": "price_pm_storage_prorate", "metadata": { "purchasable_reference": "pm-storage" } } } }, "parent": { "subscription_item_details": { "proration": true } } }
          ] }
        }
        """);

        var builder = Builder(out _);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        // Top-level "proration": true on this line is a no-op (unmapped field); nested false wins -> it's a position.
        Assert.NotNull(preview.PasswordManager.AdditionalStorage);
        Assert.Equal(2, preview.PasswordManager.AdditionalStorage!.Quantity);
        Assert.Equal(17.91m, preview.PasswordManager.AdditionalStorage.Cost);

        // Top-level "proration": false is a no-op; nested true wins -> it's a proration, not a second position.
        var proration = Assert.Single(preview.PasswordManager.Prorations!);
        Assert.Equal(12.34m, proration.Charge);
        Assert.Equal(0m, proration.Credit);
        Assert.Equal(12.34m, proration.Total);
    }

    [Fact]
    public void BuildFromInvoice_UnknownReference_LogsAndSkips()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 13290, "amount_due": 13290,
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 500, "quantity": 1, "pricing": { "price_details": { "price": { "id": "price_weird", "metadata": { "purchasable_reference": "pm-unknown" } } } } }
          ] }
        }
        """);

        var builder = Builder(out var logger);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        Assert.Null(preview.PasswordManager.AdditionalStorage);
        Assert.Contains(logger.Errors, e => e.Contains("pm-unknown"));
    }

    [Fact]
    public void BuildFromInvoice_DuplicateReference_Throws()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 13789, "amount_due": 13789,
          "lines": { "data": [
            { "amount": 12790, "quantity": 5, "pricing": { "price_details": { "price": { "id": "price_pm_seat_1", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 999, "quantity": 9, "pricing": { "price_details": { "price": { "id": "price_pm_seat_2", "metadata": { "purchasable_reference": "pm-seat" } } } } }
          ] }
        }
        """);

        var builder = Builder(out _);

        Assert.Throws<InvalidOperationException>(() => builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually));
    }

    [Fact]
    public void BuildFromInvoice_NoPasswordManagerSeats_Throws()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 4567, "amount_due": 4567,
          "lines": { "data": [
            { "amount": 4567, "quantity": 3, "pricing": { "price_details": { "price": { "id": "price_sm_seat", "metadata": { "purchasable_reference": "sm-seat" } } } } }
          ] }
        }
        """);

        var builder = Builder(out _);

        Assert.Throws<InvalidOperationException>(() => builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually));
    }

    [Fact]
    public void BuildFromInvoice_AttachesItemLevelDiscountsToTheRightItem()
    {
        var invoice = Deserialize("""
        {
          "id": "in_test", "total": 11511, "amount_due": 11511,
          "total_discount_amounts": [
            { "amount": 1279, "discount": { "id": "di_x", "source": { "coupon": { "id": "cp_x", "name": "SEATS10", "percent_off": 10, "applies_to": { "products": ["prod_pm"] } } } } }
          ],
          "lines": { "data": [
            {
              "amount": 12790, "quantity": 5,
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } },
              "discount_amounts": [ { "amount": 1279, "discount": "di_x" } ]
            }
          ] }
        }
        """);

        var builder = Builder(out _);
        var preview = builder.Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Annually);

        var discount = Assert.Single(preview.PasswordManager.Seats.Discounts!);
        Assert.Equal(BitwardenDiscountType.PercentOff, discount.Type);
        Assert.Equal(10m, discount.Value);
        Assert.Equal(12.79m, discount.Amount);
        Assert.Equal("SEATS10", discount.Label);
    }

    [Fact]
    public void BuildFromSubscription_SumsCurrentItems_ZeroTax_NoProrationsNoDiscounts()
    {
        var subscription = DeserializeSubscription("""
        {
          "id": "sub_test",
          "items": { "data": [
            { "id": "si_1", "quantity": 5, "price": { "id": "price_pm_seat", "unit_amount": 2558, "unit_amount_decimal": "2558", "metadata": { "purchasable_reference": "pm-seat" } } },
            { "id": "si_2", "quantity": 2, "price": { "id": "price_pm_storage", "unit_amount": 599, "unit_amount_decimal": "599", "metadata": { "purchasable_reference": "pm-storage" } } }
          ] }
        }
        """);

        var builder = Builder(out _);
        var preview = builder.Build(subscription, PlanTierType.Teams, PlanCadenceType.Monthly);

        Assert.Equal(5, preview.PasswordManager.Seats.Quantity);
        Assert.Equal(25.58m, preview.PasswordManager.Seats.Cost);
        Assert.Equal(2, preview.PasswordManager.AdditionalStorage!.Quantity);
        Assert.Equal(5.99m, preview.PasswordManager.AdditionalStorage.Cost);
        Assert.Equal(139.88m, preview.Total);
        Assert.Equal(139.88m, preview.AmountDue);
        Assert.Equal(0m, preview.EstimatedTax);
        Assert.Null(preview.PasswordManager.Prorations);
        Assert.Null(preview.Discounts);
    }

    [Fact]
    public void BuildFromSubscription_NoPasswordManagerSeats_Throws()
    {
        var subscription = DeserializeSubscription("""
        {
          "id": "sub_test",
          "items": { "data": [
            { "id": "si_1", "quantity": 3, "price": { "id": "price_sm_seat", "unit_amount": 4567, "unit_amount_decimal": "4567", "metadata": { "purchasable_reference": "sm-seat" } } }
          ] }
        }
        """);

        var builder = Builder(out _);

        Assert.Throws<InvalidOperationException>(() => builder.Build(subscription, PlanTierType.Teams, PlanCadenceType.Monthly));
    }

    [Fact]
    public void BuildFromSubscription_UnplaceableItem_CountsTowardTotalButIsSkipped()
    {
        var subscription = DeserializeSubscription("""
        {
          "id": "sub_test",
          "items": { "data": [
            { "id": "si_1", "quantity": 5, "price": { "id": "price_pm_seat", "unit_amount": 2558, "unit_amount_decimal": "2558", "metadata": { "purchasable_reference": "pm-seat" } } },
            { "id": "si_2", "quantity": 2, "price": { "id": "price_mystery", "unit_amount": 500, "unit_amount_decimal": "500", "metadata": {} } }
          ] }
        }
        """);

        var builder = Builder(out var logger);
        var preview = builder.Build(subscription, PlanTierType.Teams, PlanCadenceType.Monthly);

        // Only the resolvable line is placed...
        Assert.Equal("pm-seat", preview.PasswordManager.Seats.Reference);
        Assert.Null(preview.PasswordManager.AdditionalStorage);
        // ...but the unplaceable line still counts toward the total, so it is never understated (127.90 + 10.00).
        Assert.Equal(137.90m, preview.Total);
        Assert.Equal(137.90m, preview.AmountDue);
        Assert.Contains(logger.Errors, e => e.Contains("price_mystery"));
    }

    [Fact]
    public void BuildFromSubscription_DuplicateReference_Throws()
    {
        var subscription = DeserializeSubscription("""
        {
          "id": "sub_test",
          "items": { "data": [
            { "id": "si_1", "quantity": 5, "price": { "id": "price_pm_seat_1", "unit_amount": 2558, "unit_amount_decimal": "2558", "metadata": { "purchasable_reference": "pm-seat" } } },
            { "id": "si_2", "quantity": 9, "price": { "id": "price_pm_seat_2", "unit_amount": 100, "unit_amount_decimal": "100", "metadata": { "purchasable_reference": "pm-seat" } } }
          ] }
        }
        """);

        var builder = Builder(out _);

        Assert.Throws<InvalidOperationException>(() => builder.Build(subscription, PlanTierType.Teams, PlanCadenceType.Monthly));
    }

    [Fact]
    public void BuildFromSubscription_FractionalCentPrice_UsesDecimalAmount_NotZero()
    {
        // Stripe omits unit_amount for fractional-cent per-unit prices; only unit_amount_decimal is set.
        var subscription = DeserializeSubscription("""
        {
          "id": "sub_test",
          "items": { "data": [
            { "id": "si_1", "quantity": 300, "price": { "id": "price_pm_seat", "unit_amount_decimal": "0.5", "metadata": { "purchasable_reference": "pm-seat" } } }
          ] }
        }
        """);

        var builder = Builder(out _);
        var preview = builder.Build(subscription, PlanTierType.Teams, PlanCadenceType.Monthly);

        Assert.Equal(300, preview.PasswordManager.Seats.Quantity);
        Assert.Equal(0.005m, preview.PasswordManager.Seats.Cost); // 0.5¢ unit price ÷ 100 — UnitAmountDecimal avoids the old truncate-to-0
        Assert.Equal(1.50m, preview.Total); // 300 × 0.5¢ ÷ 100
    }
}
