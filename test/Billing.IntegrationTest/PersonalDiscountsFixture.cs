using Bit.Api.IntegrationTest.Factories;
using Bit.Core;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Variant of <see cref="StripeTestsFixture"/> that enables the personal discounts
/// feature flag so the <c>GET /account/billing/vnext/discounts</c> endpoint is
/// reachable and the discount-audience filter pipeline runs.
/// </summary>
public sealed class PersonalDiscountsFixture : StripeTestsFixture
{
    protected override ApiApplicationFactory CreateApi()
    {
        var api = new ApiApplicationFactory
        {
            StripeEnabled = true,
        };

        api.UpdateConfiguration(
            $"globalSettings:launchDarkly:flagValues:{FeatureFlagKeys.PM29108_EnablePersonalDiscounts}",
            "true");

        return api;
    }
}
