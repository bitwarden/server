using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class BusinessUnitConversionTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task ConvertingOrganizationToBusinessUnit_ProviderWarnings_Succeed()
    {
        var (client, providerId) = await fixture.PrepareProviderAdminAsync("provider@example.com");

        var warningsResponse = await client.GetAsync($"/providers/{providerId}/billing/vnext/warnings");
        await Assert.SuccessResponseAsync(warningsResponse);
    }
}
