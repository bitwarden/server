using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class RetrievingAccountSubscriptionPreviewTests(PreviewDrivenCartFixture fixture)
    : IClassFixture<PreviewDrivenCartFixture>
{
    [BillingFact]
    public async Task Preview_ForActivePremiumUser_ReturnsPreviewAtTheClientContractRoute()
    {
        var client = await fixture.PreparePremiumUserAsync("account-subscription-preview@example.com");

        // The client (subscription-preview.client.ts) is pinned to this exact path; this asserts
        // the corrected host mount (/account/billing/subscription) resolves it to the handler.
        var response = await client.GetAsync("/account/billing/subscription/preview");
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("active", preview["status"]!.GetValue<string>());
        Assert.NotNull(preview["invoicePreview"]);
        Assert.NotNull(preview["invoicePreview"]!["nextPaymentAttempt"]);
    }

    [BillingFact]
    public async Task Preview_ForUserWithoutSubscription_Returns404()
    {
        var (token, _) = await fixture.Api.LoginWithNewAccount("account-no-subscription-preview@example.com");
        var client = fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/account/billing/subscription/preview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
