using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Core.Billing.Constants;
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
        // An upgrade mid-term is projected as a proration (credit for the unused Premium seat against
        // the prorated Families charge), not as a full-price seats line.
        var proration = Assert.Single(preview["passwordManager"]!["prorations"]!.AsArray());
        Assert.Equal(StripeConstants.PurchasableReferences.PasswordManagerSeat, proration!["reference"]!.GetValue<string>());
        var credit = proration["credit"]!.GetValue<decimal>();
        var charge = proration["charge"]!.GetValue<decimal>();
        Assert.True(credit > 0m);
        Assert.True(charge > credit);
        Assert.Equal(charge - credit, proration["total"]!.GetValue<decimal>());
        var total = preview["total"]!.GetValue<decimal>();
        var estimatedTax = preview["estimatedTax"]!.GetValue<decimal>();
        Assert.Equal(charge - credit + estimatedTax, total);
        Assert.Equal(total, preview["amountDue"]!.GetValue<decimal>());
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
