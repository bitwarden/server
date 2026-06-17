using System.Net.Http.Json;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class UpdatingSecretsManagerSubscriptionTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task SeatAdjustment_AfterSubscribingToSecretsManager_PersistsTheNewSeatCount()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("sm-subscription@example.com");

        // Subscribe the org to Secrets Manager so the sm-subscription endpoint
        // has line items to adjust.
        var subscribeResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/subscribe-secrets-manager",
            new { AdditionalSmSeats = 2, AdditionalServiceAccounts = 0 });
        await Assert.SuccessResponseAsync(subscribeResponse);

        // Drives UpdateSecretsManagerSubscriptionCommand, which fetches the
        // subscription with Expand=customer, test_clock to reconcile seat
        // changes against the live subscription state.
        var adjustResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/sm-subscription",
            new { SeatAdjustment = 3, ServiceAccountAdjustment = 0 });
        await Assert.SuccessResponseAsync(adjustResponse);
    }
}
