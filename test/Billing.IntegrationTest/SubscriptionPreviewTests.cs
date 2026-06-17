using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class SubscriptionPreviewTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task PlanChange_ToTeamsAnnually_ReturnsTaxAndTotal()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("preview-plan-change@example.com");

        var response = await client.PostAsJsonAsync(
            $"/billing/preview-invoice/organizations/{organizationId}/subscription/plan-change",
            new
            {
                Plan = new { Tier = "Teams", Cadence = "Annually" },
                BillingAddress = new { Country = "US", PostalCode = "43432" },
            });
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(preview["tax"]);
        Assert.NotNull(preview["total"]);
    }

    [BillingFact]
    public async Task SubscriptionUpdate_WithAdditionalSeats_ReturnsTaxAndTotal()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("preview-update@example.com");

        var response = await client.PutAsJsonAsync(
            $"/billing/preview-invoice/organizations/{organizationId}/subscription/update",
            new
            {
                Update = new
                {
                    PasswordManager = new { Seats = 15 },
                },
            });
        await Assert.SuccessResponseAsync(response);

        var preview = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(preview["tax"]);
        Assert.NotNull(preview["total"]);
    }
}
