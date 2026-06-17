using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class RetrievingOrganizationBillingTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task PaymentMethod_ReturnsTheStripeVisaTestCard()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("payment-method@example.com");

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/payment-method");
        await Assert.SuccessResponseAsync(response);

        var paymentMethod = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("card", paymentMethod["type"]!.GetValue<string>());
        Assert.Equal("visa", paymentMethod["brand"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task BillingAddress_ReturnsTheAddressCapturedAtSignup()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("billing-address@example.com");

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/address");
        await Assert.SuccessResponseAsync(response);

        var billingAddress = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("US", billingAddress["country"]!.GetValue<string>());
        Assert.Equal("43432", billingAddress["postalCode"]!.GetValue<string>());
    }

    [BillingFact]
    public async Task Metadata_ReportsOrganizationIsNotOnSecretsManagerStandalone()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("metadata@example.com");

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/metadata");
        await Assert.SuccessResponseAsync(response);

        var metadata = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.False(metadata["isOnSecretsManagerStandalone"]!.GetValue<bool>());
    }

    [BillingFact]
    public async Task Warnings_ReturnsAWarningsObject()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("warnings@example.com");

        var response = await client.GetAsync($"/organizations/{organizationId}/billing/vnext/warnings");
        await Assert.SuccessResponseAsync(response);

        var warnings = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(warnings);
    }
}
