using System.Text.Json.Nodes;

namespace Bit.Billing.IntegrationTest;

public class StripeWebhookTests(StripeWebhookTestsFixture fixture) : IClassFixture<StripeWebhookTestsFixture>
{
    [BillingFact]
    public async Task CustomerUpdated_ReFetchesTheCustomerWithSubscriptionsExpanded()
    {
        // Drives CustomerUpdatedHandler -> StripeEventService.GetCustomer(parsedEvent, fresh: true, ["subscriptions"]),
        // which executes the Expand inside StripeEventService.
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-customer-updated@example.com");
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);

        await fixture.Billing.SendStripeWebhookAsync(
            "customer.updated",
            new JsonObject { ["id"] = customerId, ["object"] = "customer" },
            $"evt_{Guid.NewGuid():N}");
    }

    [BillingFact]
    public async Task SubscriptionUpdated_ReFetchesTheSubscriptionWithCustomerLatestInvoiceTestClockExpanded()
    {
        // Drives SubscriptionUpdatedHandler -> StripeEventService.GetSubscription(parsedEvent, fresh: true,
        // ["customer.discount", "discounts", "latest_invoice", "test_clock"]).
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-subscription-updated@example.com");
        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);

        await fixture.Billing.SendStripeWebhookAsync(
            "customer.subscription.updated",
            new JsonObject { ["id"] = subscriptionId, ["object"] = "subscription" },
            $"evt_{Guid.NewGuid():N}");
    }

    [BillingFact]
    public async Task InvoiceUpcoming_ReFetchesTheInvoiceWithCustomerAndSubscriptionExpanded()
    {
        // Drives UpcomingInvoiceHandler -> StripeEventService.GetInvoice (and an additional Expand call inside the handler).
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-invoice-upcoming@example.com");
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);

        // Stripe will create a real upcoming-invoice preview for this subscription on demand;
        // we synthesize the event payload with the IDs and let the handler re-fetch via the API.
        await fixture.Billing.SendStripeWebhookAsync(
            "invoice.upcoming",
            new JsonObject
            {
                ["id"] = $"in_upcoming_{Guid.NewGuid():N}",
                ["object"] = "invoice",
                ["customer"] = customerId,
                ["subscription"] = subscriptionId,
            },
            $"evt_{Guid.NewGuid():N}");
    }

    [BillingFact]
    public async Task PaymentMethodAttached_ReFetchesThePaymentMethodWithCustomerSubscriptionsExpanded()
    {
        // Drives PaymentMethodAttachedHandler -> StripeEventService.GetPaymentMethod with
        // Expand=["customer.subscriptions.data.latest_invoice"].
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-pm-attached@example.com");
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);

        await fixture.Billing.SendStripeWebhookAsync(
            "payment_method.attached",
            new JsonObject
            {
                ["id"] = "pm_card_visa",
                ["object"] = "payment_method",
                ["customer"] = customerId,
            },
            $"evt_{Guid.NewGuid():N}");
    }

    [BillingFact]
    public async Task SetupIntentSucceeded_ReFetchesTheSetupIntentWithExpand()
    {
        // Drives SetupIntentSucceededHandler -> StripeEventService.GetSetupIntent.
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-setup-intent-succeeded@example.com");

        // Create a fresh SetupIntent via the Stripe SDK so we have a real id to reference.
        var setupIntentId = await fixture.CreateBareSetupIntentAsync(
            await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId));

        await fixture.Billing.SendStripeWebhookAsync(
            "setup_intent.succeeded",
            new JsonObject { ["id"] = setupIntentId, ["object"] = "setup_intent" },
            $"evt_{Guid.NewGuid():N}");
    }

    [BillingFact]
    public async Task ChargeSucceeded_ReFetchesTheChargeFromStripe()
    {
        // Drives ChargeSucceededHandler -> StripeEventService.GetCharge.
        // Org signup with pm_card_visa produces an immediate Stripe charge; we look it up.
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-charge-succeeded@example.com");
        var chargeId = await fixture.CreateChargeForOrganizationAsync(organizationId);

        await fixture.Billing.SendStripeWebhookAsync(
            "charge.succeeded",
            new JsonObject { ["id"] = chargeId, ["object"] = "charge" },
            $"evt_{Guid.NewGuid():N}");
    }

    [BillingFact]
    public async Task CheckoutSessionCompleted_ReFetchesTheSessionWithSubscriptionExpanded()
    {
        // Drives CheckoutSessionCompletedHandler -> StripeAdapter.GetCheckoutSessionAsync ->
        // StripeEventService.GetCheckoutSession with Expand=["subscription"].
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-checkout-completed@example.com");
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);

        var sessionId = await fixture.CreateCheckoutSessionAsync(customerId);

        await fixture.Billing.SendStripeWebhookAsync(
            "checkout.session.completed",
            new JsonObject { ["id"] = sessionId, ["object"] = "checkout.session" },
            $"evt_{Guid.NewGuid():N}");
    }
}
