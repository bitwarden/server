using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class UpdatingPremiumSubscriptionTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task Storage_WhenAdditionalGbProvided_PersistsAndReturnsNewStorageState()
    {
        var client = await fixture.PreparePremiumUserAsync("premium-storage@example.com");

        // Drives UpdatePremiumStorageCommand, which fetches the subscription with
        // Expand=customer, test_clock; the customer and test_clock fields are then
        // read while adjusting storage line items + finalizing the proration invoice.
        var response = await client.PutAsJsonAsync(
            "/account/billing/vnext/subscription/storage",
            new { AdditionalStorageGb = (short)1 });
        await Assert.SuccessResponseAsync(response);

        var subscription = await client.GetAsync("/account/billing/vnext/subscription");
        await Assert.SuccessResponseAsync(subscription);
        var body = (await subscription.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(body["storage"]);
    }

    [BillingFact]
    public async Task Storage_WhenUpdated_PreservesSubscriptionMetadata()
    {
        // The SDK-bump audit verified update flows don't clear metadata they don't explicitly set.
        // The premium subscription carries a `userId` metadata key from creation; a storage update
        // (a subscription update) must not wipe it. Guards against a flow passing Metadata = {} on update.
        const string email = "premium-metadata-preserve@example.com";
        var client = await fixture.PreparePremiumUserAsync(email);

        var response = await client.PutAsJsonAsync(
            "/account/billing/vnext/subscription/storage",
            new { AdditionalStorageGb = (short)1 });
        await Assert.SuccessResponseAsync(response);

        var subscriptionId = await fixture.GetUserGatewaySubscriptionIdByEmailAsync(email);
        var metadata = await fixture.GetSubscriptionMetadataAsync(subscriptionId);
        Assert.Contains("userId", metadata.Keys);
    }

    private const string UpgradePreviewRoute =
        "/account/billing/subscription/upgrade/preview?targetProductTierType=Families&country=US&postalCode=43432";

    [BillingFact]
    public async Task UpgradePreview_ToFamiliesPlan_ReturnsTheFamiliesCartWithTax()
    {
        var client = await fixture.PreparePremiumUserAsync("premium-upgrade-preview@example.com");

        var response = await client.GetAsync(UpgradePreviewRoute);
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("families", preview["planTier"]!.GetValue<string>());
        Assert.Equal("annually", preview["cadence"]!.GetValue<string>());
        Assert.Equal(1, preview["passwordManager"]!["seats"]!["quantity"]!.GetValue<long>());
        Assert.True(preview["total"]!.GetValue<decimal>() >= 0m);
        Assert.True(preview["estimatedTax"]!.GetValue<decimal>() >= 0m);
        Assert.NotNull(preview["amountDue"]);
    }

    [BillingFact]
    public async Task UpgradePreview_WithCustomerLevelCoupon_ReducesTheTotal()
    {
        const string email = "premium-upgrade-preview-customer-coupon@example.com";
        var client = await fixture.PreparePremiumUserAsync(email);

        var baselineResponse = await client.GetAsync(UpgradePreviewRoute);
        await Assert.SuccessResponseAsync(baselineResponse);
        var baseline = (await baselineResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var baselineTotal = baseline["total"]!.GetValue<decimal>();

        var customerId = await fixture.GetUserGatewayCustomerIdByEmailAsync(email);
        var couponId = $"cust_prem_upg_{Guid.NewGuid():N}";
        await fixture.SeedAndAttachCustomerCouponAsync(customerId, couponId, percentOff: 25);

        var discountedResponse = await client.GetAsync(UpgradePreviewRoute);
        await Assert.SuccessResponseAsync(discountedResponse);
        var discounted = (await discountedResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var discountedTotal = discounted["total"]!.GetValue<decimal>();

        Assert.True(discountedTotal < baselineTotal,
            $"Expected discounted total ({discountedTotal}) to be less than baseline ({baselineTotal})");
    }
}
