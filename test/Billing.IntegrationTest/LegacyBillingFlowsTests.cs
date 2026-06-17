using System.Net.Http.Json;
using Bit.Core.Billing.Enums;
using Bit.Test.Common.Helpers;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Tests that exercise legacy billing code paths gated by PM32581 / PM37597.
/// They use <see cref="LegacyBillingFlagsFixture"/> so the flags resolve to
/// false during the host's lifetime.
/// </summary>
public class LegacyBillingFlowsTests(LegacyBillingFlagsFixture fixture) : IClassFixture<LegacyBillingFlagsFixture>
{
    [BillingFact]
    public async Task SecretsManagerSubscriptionUpdate_RoutesThroughLegacyCommand()
    {
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync("legacy-sm-subscription@example.com");

        // Subscribe to Secrets Manager first so the sm-subscription endpoint has line items.
        var subscribeResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/subscribe-secrets-manager",
            new { AdditionalSmSeats = 2, AdditionalServiceAccounts = 0 });
        await Assert.SuccessResponseAsync(subscribeResponse);

        // With PM32581 off, UpdateSecretsManagerSubscriptionCommand runs the legacy
        // Stripe path that fetches the subscription with Expand=["customer", "test_clock"].
        var adjustResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/sm-subscription",
            new { SeatAdjustment = 3, ServiceAccountAdjustment = 0 });
        await Assert.SuccessResponseAsync(adjustResponse);
    }

    [BillingFact]
    public async Task PlanUpgrade_FromFamilies_ReusesCustomerViaFinalize()
    {
        // Families org → existing Stripe customer. With PM32581 off the legacy
        // UpgradeOrganizationPlanCommand path runs and calls OrganizationBillingService
        // .Finalize → GetCustomerWhileEnsuringCorrectTaxExemptionAsync.
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync(
                "legacy-plan-upgrade@example.com", PlanType.FamiliesAnnually);

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

    [BillingFact]
    public async Task PlanUpgrade_FromFamilies_ReconcilesTaxExemptionForNonUsAddress()
    {
        // Drive the legacy tax-exemption reconciliation branch: PM37597 must be off AND the
        // new plan must be Teams/Enterprise AND customer.TaxExempt must differ from the
        // determined value (forced by using a non-US billing address where Bitwarden has no
        // tax registration, which flips TaxExempt to Reverse).
        var (client, _, organizationId, _) =
            await fixture.PrepareOrganizationOwnerAsync(
                "legacy-tax-reconcile@example.com", PlanType.FamiliesAnnually);

        // Move the Families org's address out of US so the determined exemption changes.
        var addressResponse = await client.PutAsJsonAsync(
            $"/organizations/{organizationId}/billing/vnext/address",
            new { Country = "BR", PostalCode = "01310-100" });
        await Assert.SuccessResponseAsync(addressResponse);

        var upgradeResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/upgrade",
            new
            {
                PlanType = PlanType.EnterpriseAnnually,
                AdditionalSeats = 5,
                UseSecretsManager = false,
                BillingAddressCountry = "BR",
                BillingAddressPostalCode = "01310-100",
            });
        await Assert.SuccessResponseAsync(upgradeResponse);
    }
}
