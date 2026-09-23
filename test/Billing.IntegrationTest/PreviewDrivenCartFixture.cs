using Bit.Api.IntegrationTest.Factories;
using Bit.Invoicing;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Variant of <see cref="StripeTestsFixture"/> that enables the preview-driven cart
/// feature flag so the GET /account/billing/subscription/preview endpoint is reachable.
/// </summary>
public sealed class PreviewDrivenCartFixture : StripeTestsFixture
{
    protected override ApiApplicationFactory CreateApi()
    {
        var api = new ApiApplicationFactory
        {
            StripeEnabled = true,
        };

        api.UpdateConfiguration(
            $"globalSettings:launchDarkly:flagValues:{InvoicingFeatureFlags.PM36631_PreviewDrivenCart}",
            "true");

        return api;
    }
}
