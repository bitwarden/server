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
}
