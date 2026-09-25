using Bit.Api.IntegrationTest.Factories;
using Bit.Core;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Variant of <see cref="StripeTestsFixture"/> that turns off the feature flags
/// gating the vnext billing flow, so tests can drive the legacy
/// <see cref="OrganizationBillingService.Finalize"/> and
/// <c>UpdateSecretsManagerSubscriptionCommand</c> Stripe paths.
/// </summary>
public sealed class LegacyBillingFlagsFixture : StripeTestsFixture
{
    protected override ApiApplicationFactory CreateApi()
    {
        var api = new ApiApplicationFactory
        {
            StripeEnabled = true,
        };

        api.UpdateConfiguration(
            $"globalSettings:launchDarkly:flagValues:{FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand}",
            "false");

        return api;
    }
}
