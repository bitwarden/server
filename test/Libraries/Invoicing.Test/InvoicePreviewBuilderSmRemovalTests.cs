using Bit.Core.Billing.Enums;
using Bit.Invoicing.InvoicePreviews;
using Bit.Invoicing.InvoicePreviews.Models;
using Stripe;
using Xunit;

namespace Bit.Invoicing.Test;

public class InvoicePreviewBuilderSmRemovalTests
{
    private static InvoicePreviewBuilder Builder() => new(new RecordingLogger<InvoicePreviewBuilder>());

    // Shape captured live from Stripe create_preview for a mid-cycle Secrets Manager removal:
    // an sm-seat proration credit (proration = true) with no recurring sm-seat line; total already nets the credit.
    [Fact]
    public void BuildFromInvoice_SmRemovedMidCycle_RendersProrationOnlySectionAndReconciles()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_preview_sm_removal", "total": 3452, "amount_due": 3452, "period_end": 1789769920,
          "lines": { "data": [
            { "amount": -1548, "quantity": 3,
              "parent": { "subscription_item_details": { "proration": true }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_sm_seat", "metadata": { "purchasable_reference": "sm-seat" } } } },
              "period": { "start": 1788387520, "end": 1789769920 } },
            { "amount": 5000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "unit_amount_decimal": "1000", "metadata": { "purchasable_reference": "pm-seat" } } } },
              "period": { "start": 1789769920, "end": 1792361920 } }
          ] }
        }
        """);

        var preview = Builder().Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Monthly);

        Assert.Equal(10.00m, preview.PasswordManager.Seats.Cost);
        Assert.Equal(34.52m, preview.Total);
        Assert.Equal(34.52m, preview.AmountDue);

        Assert.NotNull(preview.SecretsManager);
        Assert.Null(preview.SecretsManager!.Seats);
        var proration = Assert.Single(preview.SecretsManager.Prorations!);
        Assert.Equal(15.48m, proration.Credit);
        Assert.Equal(-15.48m, proration.Total);
        Assert.Equal(1, proration.Months);

        // Visible rows now reconcile to the invoice total (unit cost × quantity, plus the proration).
        Assert.Equal(
            preview.Total,
            preview.PasswordManager.Seats.Quantity * preview.PasswordManager.Seats.Cost + proration.Total);
    }

    // A plain Password Manager invoice with no Secrets Manager line and no Secrets Manager proration
    // must still yield no Secrets Manager section (the other half of the guard).
    [Fact]
    public void BuildFromInvoice_NoSecretsManagerActivity_LeavesSectionNull()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_pm_only", "total": 5000, "amount_due": 5000,
          "lines": { "data": [
            { "amount": 5000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "metadata": { "purchasable_reference": "pm-seat" } } } } }
          ] }
        }
        """);

        var preview = Builder().Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Monthly);

        Assert.Null(preview.SecretsManager);
    }

    // A resolved sm-service-account line with no sm-seat line and no proration must keep the section,
    // otherwise the service-account cost would silently drop out of the reconciled total.
    [Fact]
    public void BuildFromInvoice_ServiceAccountsWithoutSeatsLine_KeepsSectionAndReconciles()
    {
        var invoice = Invoice.FromJson("""
        {
          "id": "in_sm_service_accounts_only", "total": 6000, "amount_due": 6000,
          "lines": { "data": [
            { "amount": 5000, "quantity": 5,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_pm_seat", "unit_amount_decimal": "1000", "metadata": { "purchasable_reference": "pm-seat" } } } } },
            { "amount": 1000, "quantity": 2,
              "parent": { "subscription_item_details": { "proration": false }, "type": "subscription_item_details" },
              "pricing": { "price_details": { "price": { "id": "price_sm_service_account", "unit_amount_decimal": "500", "metadata": { "purchasable_reference": "sm-service-account" } } } } }
          ] }
        }
        """);

        var preview = Builder().Build(invoice, PlanTierType.Enterprise, PlanCadenceType.Monthly);

        Assert.NotNull(preview.SecretsManager);
        Assert.Null(preview.SecretsManager!.Seats);
        Assert.Null(preview.SecretsManager.Prorations);
        var serviceAccounts = preview.SecretsManager.AdditionalServiceAccounts;
        Assert.NotNull(serviceAccounts);
        Assert.Equal(5.00m, serviceAccounts!.Cost);

        // The service-account row reconciles into the total alongside Password Manager seats (unit cost × quantity).
        Assert.Equal(
            preview.Total,
            preview.PasswordManager.Seats.Quantity * preview.PasswordManager.Seats.Cost
                + serviceAccounts.Quantity * serviceAccounts.Cost);
    }
}
