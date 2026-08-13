using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Core.Billing.Enums;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class UpdatingPersonalBillingAddressTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task BillingAddress_ForFamiliesOrganization_PersistsViaPersonalPath()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("families-billing-address@example.com", PlanType.FamiliesAnnually);

        // Families is a personal-tier product, so UpdateBillingAddressCommand
        // dispatches to UpdatePersonalBillingAddressAsync, which expands
        // ["subscriptions", "subscriptions.data.test_clock"] and then walks
        // customer.Subscriptions to enable automatic tax on the active sub.
        var response = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/billing/vnext/address",
            new
            {
                Country = "US",
                PostalCode = "10001",
                Line1 = "1 Family Way",
                City = "New York",
                State = "NY",
            });
        await Assert.SuccessResponseAsync(response);

        var billingAddress = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("US", billingAddress["country"]!.GetValue<string>());
        Assert.Equal("10001", billingAddress["postalCode"]!.GetValue<string>());
    }
}
