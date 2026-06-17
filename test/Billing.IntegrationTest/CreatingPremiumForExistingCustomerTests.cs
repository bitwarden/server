using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class CreatingPremiumForExistingCustomerTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task RecreatingPremium_AfterCancellation_HitsExistingCustomerBranch()
    {
        const string email = "premium-resubscribe-existing-customer@example.com";
        var client = await fixture.PreparePremiumUserAsync(email);

        var profileResponse = await client.GetAsync("/accounts/profile");
        await Assert.SuccessResponseAsync(profileResponse);
        var userId = (await profileResponse.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<Guid>();

        // Cancel the existing premium subscription immediately so the user keeps their
        // GatewayCustomerId but has a Canceled (terminal) Stripe subscription.
        await fixture.CancelUserSubscriptionImmediatelyAsync(userId);

        // Re-purchase premium — CreatePremiumCloudHostedSubscriptionCommand sees an
        // existing GatewayCustomerId plus a terminal subscription, so it takes the
        // line-127 branch (updatePaymentMethod + GetCustomer with Expand=_expand).
        var resubscribeResponse = await client.PostAsJsonAsync(
            "/account/billing/vnext/subscription",
            new
            {
                TokenizedPaymentMethod = new { Type = "card", Token = "pm_card_visa" },
                BillingAddress = new { Country = "US", PostalCode = "43432" },
                AdditionalStorageGb = 0,
            });
        await Assert.SuccessResponseAsync(resubscribeResponse);
    }
}
