using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class RetrievingPremiumSubscriptionTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task Subscription_ForActivePremiumUser_ReturnsTheCanonicalCartAndStorage()
    {
        var client = await fixture.PreparePremiumUserAsync("premium-subscription@example.com");

        // Drives GetBitwardenSubscriptionQuery.FetchSubscriptionAsync, which lists
        // schedules with Expand=phases.discounts.coupon.applies_to and reads the
        // subscription's items / coupons. Cart shape requires both expand paths
        // to be honored or the response would shed line items / discounts.
        var response = await client.GetAsync("/account/billing/vnext/subscription");
        await Assert.SuccessResponseAsync(response);

        var subscription = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("active", subscription["status"]!.GetValue<string>());
        Assert.NotNull(subscription["cart"]);
        Assert.NotNull(subscription["storage"]);
        Assert.NotNull(subscription["nextCharge"]);
    }

    [BillingFact]
    public async Task PaymentMethod_ForPremiumUser_ReturnsTheStripeVisaTestCard()
    {
        var client = await fixture.PreparePremiumUserAsync("premium-payment-method@example.com");

        var response = await client.GetAsync("/account/billing/vnext/payment-method");
        await Assert.SuccessResponseAsync(response);

        var paymentMethod = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("card", paymentMethod["type"]!.GetValue<string>());
        Assert.Equal("visa", paymentMethod["brand"]!.GetValue<string>());
    }
}
