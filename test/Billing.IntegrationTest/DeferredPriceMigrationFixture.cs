using Bit.Api.IntegrationTest.Factories;
using Bit.Core;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Variant of <see cref="StripeTestsFixture"/> that enables
/// <see cref="FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal"/>, so tests can drive
/// <see cref="Bit.Core.Billing.Pricing.IPriceIncreaseScheduler"/> to create the active 2-phase
/// deferred-migration schedule that the customer-coupon carry branches depend on.
/// </summary>
public sealed class DeferredPriceMigrationFixture : StripeTestsFixture
{
    protected override ApiApplicationFactory CreateApi()
    {
        var api = new ApiApplicationFactory
        {
            StripeEnabled = true,
        };

        api.UpdateConfiguration("globalSettings:stripe:maxNetworkRetries", "5");
        api.UpdateConfiguration(
            $"globalSettings:launchDarkly:flagValues:{FeatureFlagKeys.PM32645_DeferPriceMigrationToRenewal}",
            "true");

        return api;
    }
}
