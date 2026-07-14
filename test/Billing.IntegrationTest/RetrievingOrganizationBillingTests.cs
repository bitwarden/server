using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class RetrievingOrganizationBillingTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task PaymentMethod_ReturnsTheStripeVisaTestCard()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("payment-method@example.com");

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/payment-method");
        await Assert.SuccessResponseAsync(response);

        var paymentMethod = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("card", paymentMethod["type"]!.GetValue<string>());
        Assert.Equal("visa", paymentMethod["brand"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task BillingAddress_ReturnsTheAddressCapturedAtSignup()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("billing-address@example.com");

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/address");
        await Assert.SuccessResponseAsync(response);

        var billingAddress = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("US", billingAddress["country"]!.GetValue<string>());
        Assert.Equal("43432", billingAddress["postalCode"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task Metadata_ReportsOrganizationIsNotOnSecretsManagerStandalone()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("metadata@example.com");

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/metadata");
        await Assert.SuccessResponseAsync(response);

        var metadata = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.False(metadata["isOnSecretsManagerStandalone"]!.GetValue<bool>());
    }


    [BillingFact]
    public async Task Metadata_WithSecretsManagerStandaloneCoupon_ReportsIsOnSecretsManagerStandalone()
    {
        // GetOrganizationMetadataQuery.IsOnSecretsManagerStandalone requires
        // (a) the org's plan supports SM,
        // (b) a subscription-level discount references the "sm-standalone" coupon,
        // (c) the coupon's applies_to.products intersects with a product on the sub.
        // The path fetches the subscription with `discounts.source.coupon.applies_to`,
        // reads `d.Source.Coupon.Id`, and checks `Coupon.AppliesTo.Products`. This
        // exercises the (still 4-level) sub-level discount expand end-to-end — the
        // regression detector for whether AppliesTo survives the SDK bump's read path.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("metadata-sm-standalone@example.com");

        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);
        var productId = await fixture.GetOrganizationFirstProductIdAsync(organizationId);

        await fixture.SeedAndAttachSubscriptionCouponAsync(
            subscriptionId,
            "sm-standalone",
            percentOff: 100,
            scopedToProductId: productId);

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/metadata");
        await Assert.SuccessResponseAsync(response);

        var metadata = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.True(metadata["isOnSecretsManagerStandalone"]!.GetValue<bool>());
    }

    [BillingFact]
    public async Task Subscription_WithCustomerLevelPercentOffCoupon_ReturnsDiscountPercentage()
    {
        // Drives OrganizationsController.GetSubscription + StripePaymentService.GetSubscriptionAsync.
        // The response's CustomerDiscount.PercentOff reads `discount.PercentOff` where `discount`
        // is `subscription.Customer?.Discount ?? subscription.Discounts?.FirstOrDefault()` — i.e.
        // `Customer.Discount.Source.Coupon.PercentOff` for the customer-level branch. Requires
        // `customer.discount.source.coupon` in the expand; without it the coupon comes back as an
        // unexpanded stub and PercentOff is null (the 2025-09-30.clover Discount refactor moved
        // Coupon under Discount.Source). This is the value assertion that catches that regression.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("org-customer-coupon@example.com");

        var customerId = await fixture.GetOrganizationGatewayCustomerIdAsync(organizationId);
        var couponId = $"org_cust_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, couponId, percentOff: 30);

        var response = await client.GetAsync($"/organizations/{organizationId}/subscription");
        await Assert.SuccessResponseAsync(response);

        var subscription = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(30m, subscription["customerDiscount"]!["percentOff"]!.GetValue<decimal>());
    }

    [BillingFact]
    public async Task Subscription_IsCreatedWithClassicBillingMode()
    {
        // The SDK bump sets BillingMode = { Type = "classic" } on subscription creation
        // (OrganizationBillingService). This verifies that mode actually lands on the Stripe
        // subscription rather than defaulting to Stripe's newer "flexible" billing mode.
        var (_, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("org-billing-mode@example.com");

        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);
        var billingModeType = await fixture.GetSubscriptionBillingModeTypeAsync(subscriptionId);

        Assert.Equal("classic", billingModeType);
    }

    [BillingFact]
    public async Task Warnings_ReturnsAWarningsObject()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("warnings@example.com");

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/warnings");
        await Assert.SuccessResponseAsync(response);

        var warnings = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(warnings);
    }
}
