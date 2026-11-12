using System.Text.Json.Nodes;
using Bit.Core.Enums;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Drives the non-charge cash application payment handlers through the Billing host against a real Stripe test
/// account. Each handler re-fetches from Stripe with an Expand list and reads the expanded fields back:
/// <list type="bullet">
/// <item><c>invoice_payment.paid</c>: the invoice payment with <c>invoice</c> (owner, customer), and the cloud-region
/// check with <c>invoice.customer</c>.</item>
/// <item><c>invoice.paid</c>: the invoice with <c>payments</c> (the non-charge cash application payments to record),
/// and the cloud-region check with <c>customer</c>.</item>
/// </list>
/// The test invoices have no subscription link, as when a subscription invoice loses it, so the owner also comes
/// through the fallback to the customer's subscription.
/// </summary>
public class PostingStripeCashApplicationTransactionsTests(StripeCashApplicationFixture fixture)
    : IClassFixture<StripeCashApplicationFixture>
{
    [BillingFact]
    public async Task InvoicePaymentPaid_NonChargeWire_PostsTransactionForTheOrganization()
    {
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("cash-application-invoice-payment-paid@example.com");
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var (_, invoicePaymentId) =
            await fixture.ApplyNonChargeCashApplicationPaymentToNewInvoiceAsync(
                customerId, 15000, "Wire", "11/10/2026 - EXAMPLE PAYER LTD");

        // Real events carry the payment type, which the handler checks before calling Stripe.
        await fixture.Billing.SendStripeWebhookAsync(
            "invoice_payment.paid",
            new JsonObject
            {
                ["id"] = invoicePaymentId,
                ["object"] = "invoice_payment",
                ["payment"] = new JsonObject { ["type"] = "payment_record" },
            },
            $"evt_{Guid.NewGuid():N}");

        var transaction = await fixture.GetNonChargeCashApplicationTransactionAsync(invoicePaymentId);
        Assert.NotNull(transaction);
        Assert.Equal(organizationId, transaction.OrganizationId);
        Assert.Null(transaction.UserId);
        Assert.Null(transaction.ProviderId);
        Assert.Equal(150M, transaction.Amount);
        Assert.Equal(TransactionType.Credit, transaction.Type);
        Assert.Equal(GatewayType.Bank, transaction.Gateway);
        Assert.Equal(PaymentMethodType.WireTransfer, transaction.PaymentMethodType);
        Assert.Equal("11/10/2026 In Wire - EXAMPLE PAYER LTD $150.00", transaction.Details);

        // Redelivery never adds or changes a row.
        await fixture.Billing.SendStripeWebhookAsync(
            "invoice_payment.paid",
            new JsonObject
            {
                ["id"] = invoicePaymentId,
                ["object"] = "invoice_payment",
                ["payment"] = new JsonObject { ["type"] = "payment_record" },
            },
            $"evt_{Guid.NewGuid():N}");
        var afterRedelivery = await fixture.GetNonChargeCashApplicationTransactionAsync(invoicePaymentId);
        Assert.Equal(transaction.Id, afterRedelivery!.Id);
        Assert.Equal(transaction.Amount, afterRedelivery.Amount);
    }

    [BillingFact]
    public async Task InvoicePaid_InvoiceWithoutSubscriptionLinkWithNonChargeCheck_ReadsPaymentsAndPostsTransaction()
    {
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("cash-application-invoice-paid@example.com");
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var (invoiceId, invoicePaymentId) =
            await fixture.ApplyNonChargeCashApplicationPaymentToNewInvoiceAsync(customerId, 9900, "CHECK", "1001");

        // The handler reads parent and amount_paid from the event payload before fetching the invoice.
        await fixture.Billing.SendStripeWebhookAsync(
            "invoice.paid",
            new JsonObject
            {
                ["id"] = invoiceId,
                ["object"] = "invoice",
                ["customer"] = customerId,
                ["amount_paid"] = 9900,
                ["parent"] = null,
            },
            $"evt_{Guid.NewGuid():N}");

        var transaction = await fixture.GetNonChargeCashApplicationTransactionAsync(invoicePaymentId);
        Assert.NotNull(transaction);
        Assert.Equal(organizationId, transaction.OrganizationId);
        Assert.Equal(99M, transaction.Amount);
        Assert.Equal(PaymentMethodType.Check, transaction.PaymentMethodType);
        // The customer name segment depends on the test customer's Stripe name, so only the fixed parts are asserted.
        Assert.StartsWith("Check In", transaction.Details);
        Assert.EndsWith("#1001 $99.00", transaction.Details);
    }
}
