using System.Net.Http.Json;
using Bit.Core.Billing.Enums;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

public class UpgradingOrganizationPlanTests(StripeTestsFixture fixture) : IClassFixture<StripeTestsFixture>
{
    [BillingFact]
    public async Task FamiliesToEnterprise_ReusesTheExistingStripeCustomer()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("upgrade-families-to-enterprise@example.com", PlanType.FamiliesAnnually);

        // Drives UpgradeOrganizationPlanVNextCommand, which (for an org that
        // already has a Stripe customer + subscription) routes through
        // UpdateOrganizationSubscriptionCommand and fetches the subscription
        // with Expand=customer, test_clock to read subscription.Customer for
        // tax reconciliation against the new plan's price.
        var response = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/upgrade",
            new
            {
                PlanType = PlanType.EnterpriseAnnually,
                AdditionalSeats = 5,
                UseSecretsManager = false,
                BillingAddressCountry = "US",
                BillingAddressPostalCode = "43432",
            });
        await Assert.SuccessResponseAsync(response);
    }
}
