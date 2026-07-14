using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class RetrievingProviderBillingTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task Subscription_ReturnsTheCanonicalProviderSubscriptionFields()
    {
        var (client, providerId) = await fixture.PrepareProviderAdminAsync("provider-subscription@example.com");

        var response = await client.GetAsync($"/providers/{providerId}/billing/subscription");
        await Assert.SuccessResponseAsync(response);

        // Drives ProviderBillingController.GetSubscriptionAsync, which reads
        // subscription.Customer (customer.tax_ids expand), subscription.Discounts
        // (discounts expand) and uses test_clock to compute the period end.
        var subscription = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("trialing", subscription["status"]!.GetValue<string>());
        Assert.NotNull(subscription["currentPeriodEndDate"]);
        Assert.NotNull(subscription["taxInformation"]);
        Assert.NotNull(subscription["plans"]);
        Assert.NotNull(subscription["paymentSource"]);
    }

    [BillingFact]
    public async Task BillingAddress_ReturnsTheAddressCapturedAtSignup()
    {
        var (client, providerId) = await fixture.PrepareProviderAdminAsync("provider-address@example.com");

        var response = await client.GetAsync($"/providers/{providerId}/billing/vnext/address");
        await Assert.SuccessResponseAsync(response);

        var billingAddress = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("US", billingAddress["country"]!.GetValue<string>());
        Assert.Equal("43432", billingAddress["postalCode"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task PaymentMethod_ReturnsTheStripeVisaTestCard()
    {
        var (client, providerId) = await fixture.PrepareProviderAdminAsync("provider-payment-method@example.com");

        var response = await client.GetAsync($"/providers/{providerId}/billing/vnext/payment-method");
        await Assert.SuccessResponseAsync(response);

        var paymentMethod = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("card", paymentMethod["type"]!.GetValue<string>());
        Assert.Equal("visa", paymentMethod["brand"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task Subscription_WithCustomerLevelPercentOffCoupon_ReturnsDiscountPercentage()
    {
        // Drives ProviderBillingController.GetSubscriptionAsync + ProviderSubscriptionResponse.
        // The response's DiscountPercentage reads `discount?.Source?.Coupon?.PercentOff`
        // where `discount` is `subscription.Customer?.Discount ?? subscription.Discounts?.FirstOrDefault()`.
        // Requires `customer.discount.source.coupon` in the expand (4 levels — Stripe's cap)
        // for the customer-level branch of that null-coalescing selector.
        var (client, providerId) = await fixture.PrepareProviderAdminAsync("provider-customer-coupon@example.com");

        var customerId = await fixture.GetProviderGatewayCustomerIdAsync(providerId);
        var couponId = $"prov_cust_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, couponId, percentOff: 30);

        var response = await client.GetAsync($"/providers/{providerId}/billing/subscription");
        await Assert.SuccessResponseAsync(response);

        var subscription = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(30m, subscription["discountPercentage"]!.GetValue<decimal>());
    }

    [BillingFact]
    public async Task Subscription_IsCreatedWithClassicBillingMode()
    {
        // The SDK bump sets BillingMode = { Type = "classic" } on the provider subscription
        // (ProviderBillingService.SetupSubscription). Verifies the mode lands on Stripe.
        var (_, providerId) = await fixture.PrepareProviderAdminAsync("provider-billing-mode@example.com");

        var subscriptionId = await fixture.GetProviderGatewaySubscriptionIdAsync(providerId);
        var billingModeType = await fixture.GetSubscriptionBillingModeTypeAsync(subscriptionId);

        Assert.Equal("classic", billingModeType);
    }
}
