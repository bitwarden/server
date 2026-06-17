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
    public async Task UpgradePreview_ToFamiliesPlan_ReturnsTheProratedPricing()
    {
        var client = await fixture.PreparePremiumUserAsync("premium-upgrade-preview@example.com");

        // Drives PreviewPremiumUpgradeProrationCommand, which fetches the
        // existing premium subscription with Expand=customer to read
        // subscription.Customer for tax + billing context.
        var response = await client.PostAsJsonAsync(
            "/billing/preview-invoice/premium/subscriptions/upgrade",
            new
            {
                TargetProductTierType = "Families",
                BillingAddress = new { Country = "US", PostalCode = "43432" },
            });
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(preview["newPlanProratedAmount"]);
        Assert.NotNull(preview["credit"]);
    }
}
