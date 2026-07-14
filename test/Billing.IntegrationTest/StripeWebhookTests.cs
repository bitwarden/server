using System.Text.Json.Nodes;
using Bit.Core.Billing.Enums;

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
    public async Task SubscriptionUpdated_WhenSecretsManagerTrialRemoved_RemovesTheStandaloneCoupon()
    {
        // Drives SubscriptionUpdatedHandler.RemovePasswordManagerCouponIfRemovingSecretsManagerTrialAsync.
        // Production applies the "sm-standalone" trial coupon to subscription.Discounts at signup
        // (OrganizationBillingService applies CustomerSetup.SystemCoupons via SubscriptionDiscountOptions),
        // so SeedAndAttachSubscriptionCouponAsync reproduces the real shape. The handler re-fetches the
        // subscription and reads `Discounts[].Source.Coupon.Id` to detect the trial, then deletes it. That
        // read requires `discounts.source.coupon` in the re-fetch expand (the 2025-09-30.clover refactor
        // wrapped Coupon under Discount.Source); with only `discounts` the coupon is an unexpanded stub,
        // Id is null, and the removal silently no-ops. Asserting the coupon is actually gone catches that.
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-sm-trial-removal@example.com", PlanType.EnterpriseAnnually);
        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);

        // Attach the SM-standalone trial coupon to the (PM-only) subscription, as signup would.
        await fixture.SeedAndAttachSubscriptionCouponAsync(subscriptionId, "sm-standalone", percentOff: 100);

        // Simulate a subscription.updated where the PREVIOUS attributes carried an SM seat item and the
        // current (live) subscription does not — i.e. Secrets Manager was just removed.
        var secretsManagerSeatPlanId = await fixture.GetSecretsManagerSeatPlanIdAsync(PlanType.EnterpriseAnnually);

        await fixture.Billing.SendStripeWebhookAsync(
            "customer.subscription.updated",
            new JsonObject { ["id"] = subscriptionId, ["object"] = "subscription" },
            $"evt_{Guid.NewGuid():N}",
            previousAttributes: new JsonObject
            {
                ["items"] = new JsonObject
                {
                    ["object"] = "list",
                    ["data"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = $"si_{Guid.NewGuid():N}",
                            ["object"] = "subscription_item",
                            ["quantity"] = 1,
                            ["plan"] = new JsonObject
                            {
                                ["id"] = secretsManagerSeatPlanId,
                                ["object"] = "plan",
                            },
                            ["price"] = new JsonObject
                            {
                                ["id"] = secretsManagerSeatPlanId,
                                ["object"] = "price",
                            },
                        },
                    },
                },
            });

        var couponIds = await fixture.GetSubscriptionDiscountCouponIdsAsync(subscriptionId);
        Assert.DoesNotContain("sm-standalone", couponIds);
    }

    [BillingFact]
    public async Task SubscriptionUpdated_WhenSecretsManagerTrialRemoved_RemovesTheCustomerLevelStandaloneCoupon()
    {
        // Customer-level variant of the SM-trial removal path in SubscriptionUpdatedHandler:
        // `customerHasSecretsManagerTrial` reads subscription.Customer.Discount.Source.Coupon.Id and
        // calls DeleteCustomerDiscountAsync. Same clover Source.Coupon expand dependency as the
        // subscription-level path (guards the `customer.discount.source.coupon` half of the fix).
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("webhook-sm-trial-customer@example.com", PlanType.EnterpriseAnnually);
        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);
        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);

        // Attach the SM-standalone coupon at the customer level.
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, "sm-standalone", percentOff: 100);

        var secretsManagerSeatPlanId = await fixture.GetSecretsManagerSeatPlanIdAsync(PlanType.EnterpriseAnnually);

        await fixture.Billing.SendStripeWebhookAsync(
            "customer.subscription.updated",
            new JsonObject { ["id"] = subscriptionId, ["object"] = "subscription" },
            $"evt_{Guid.NewGuid():N}",
            previousAttributes: new JsonObject
            {
                ["items"] = new JsonObject
                {
                    ["object"] = "list",
                    ["data"] = new JsonArray
                    {
                        new JsonObject { ["plan"] = new JsonObject { ["id"] = secretsManagerSeatPlanId, ["object"] = "plan" } },
                    },
                },
            });

        Assert.False(await fixture.CustomerHasDiscountAsync(customerId));
    }

    [BillingFact]
    public async Task InvoiceCreated_WithSubscriptionLevelPercentOffCoupon_RecordsDiscountedLineItemTotals()
    {
        // Drives ProviderEventService.TryRecordInvoiceLineItems for invoice.created. The handler re-fetches
        // the invoice and computes each ProviderInvoiceItem.Total using
        // `invoice.Discounts[].Source.Coupon.PercentOff`. That read requires `discounts.source.coupon` in
        // the GetInvoice expand (the 2025-09-30.clover refactor wrapped Coupon under Discount.Source); with
        // only `discounts` the coupon is a stub, PercentOff is null, totalPercentOff is 0, and the recorded
        // Total silently omits the discount. Asserting the persisted Total is discounted catches that.
        const decimal percentOff = 25m;

        var (_, providerId) = await fixture.PrepareProviderAdminAsync("webhook-invoice-created-coupon@example.com");
        var subscriptionId = await fixture.GetProviderGatewaySubscriptionIdAsync(providerId);

        // Attach a percent-off coupon to the provider subscription, then end the trial so Stripe cuts a real
        // subscription-cycle invoice that carries the discount.
        await fixture.SeedAndAttachSubscriptionCouponAsync(subscriptionId, $"prov_inv_{Guid.NewGuid():N}", percentOff);
        var invoiceId = await fixture.EndTrialAndGetLatestInvoiceIdAsync(subscriptionId);

        await fixture.Billing.SendStripeWebhookAsync(
            "invoice.created",
            new JsonObject { ["id"] = invoiceId, ["object"] = "invoice" },
            $"evt_{Guid.NewGuid():N}");

        var seatPrice = await fixture.GetProviderPortalSeatPriceAsync(providerId);
        var items = await fixture.GetProviderInvoiceItemsAsync(providerId, invoiceId);

        Assert.NotEmpty(items);
        Assert.Contains(
            items,
            item => item.AssignedSeats > 0 && item.Total == item.AssignedSeats * seatPrice * (100 - percentOff) / 100);
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
