using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class AddingOrganizationTaxIdTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task BillingAddress_WhenTaxIdProvided_PersistsAndEchoesTaxId()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("update-billing-address-tax-id@example.com");

        // Drives UpdateBillingAddressCommand.UpdateBusinessBillingAddressAsync, which expands
        // tax_ids on the customer update so the existing-tax-id deletion loop can run, then
        // CreateTaxIdAsync attaches the new id and the response carries it back.
        var response = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/billing/vnext/address",
            new
            {
                Country = "US",
                PostalCode = "10001",
                Line1 = "123 Test St",
                City = "New York",
                State = "NY",
                TaxId = new { Code = "us_ein", Value = "12-3456789" },
            });
        await Assert.SuccessResponseAsync(response);

        var billingAddress = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("us_ein", billingAddress["taxId"]!["code"]!.GetValue<string>());
        Assert.Equal("12-3456789", billingAddress["taxId"]!["value"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task BillingAddress_AfterTaxIdAdded_GetAddressReturnsTheTaxId()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("get-billing-address-tax-id@example.com");

        var updateResponse = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/billing/vnext/address",
            new
            {
                Country = "US",
                PostalCode = "10001",
                TaxId = new { Code = "us_ein", Value = "12-3456789" },
            });
        await Assert.SuccessResponseAsync(updateResponse);

        // Drives GetBillingAddressQuery.Run business path (Expand=tax_ids), which reads
        // customer.TaxIds.FirstOrDefault() to populate the response.
        var getResponse = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/address");
        await Assert.SuccessResponseAsync(getResponse);

        var billingAddress = (await getResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("us_ein", billingAddress["taxId"]!["code"]!.GetValue<string>());
        Assert.Equal("12-3456789", billingAddress["taxId"]!["value"]!.GetValue<string>());
    }
}
