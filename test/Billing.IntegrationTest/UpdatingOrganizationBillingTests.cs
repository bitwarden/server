using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class UpdatingOrganizationBillingTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task BillingAddress_WhenAddressFieldsProvided_PersistsAllFields()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("update-billing-address@example.com");

        var response = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/billing/vnext/address",
            new
            {
                Country = "US",
                PostalCode = "10001",
                Line1 = "123 Test St",
                City = "New York",
                State = "NY",
            });
        await Assert.SuccessResponseAsync(response);

        var billingAddress = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("US", billingAddress["country"]!.GetValue<string>());
        Assert.Equal("10001", billingAddress["postalCode"]!.GetValue<string>());
        Assert.Equal("123 Test St", billingAddress["line1"]!.GetValue<string>());
        Assert.Equal("New York", billingAddress["city"]!.GetValue<string>());
        Assert.Equal("NY", billingAddress["state"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task RestartSubscription_CreatesReplacementWithClassicBillingMode()
    {
        // RestartSubscriptionCommand requires a canceled subscription and creates a replacement with
        // BillingMode = classic. Verifies that mode lands on the restarted Stripe subscription.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("org-restart-billing-mode@example.com");

        await fixture.CancelOrganizationSubscriptionAsync(organizationId);

        // The restart endpoint chains a payment-method update; pass a fresh, attachable card pm
        // (what a real client sends after tokenizing a card — the shared pm_card_visa can't be
        // attached to an existing customer).
        var cardToken = await fixture.CreateCardPaymentMethodAsync();

        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/billing/vnext/subscription/restart",
            new
            {
                PaymentMethod = new { Type = "card", Token = cardToken },
                BillingAddress = new { Country = "US", PostalCode = "43432" },
            });
        await Assert.SuccessResponseAsync(response);

        var subscriptionId = await fixture.GetOrganizationGatewaySubscriptionIdAsync(organizationId);
        var billingModeType = await fixture.GetSubscriptionBillingModeTypeAsync(subscriptionId);
        Assert.Equal("classic", billingModeType);
    }
}
