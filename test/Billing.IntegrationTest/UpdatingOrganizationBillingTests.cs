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
}
