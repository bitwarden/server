using Bit.Core;

namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Variant of <see cref="StripeWebhookTestsFixture"/> whose Billing host has the
/// <see cref="FeatureFlagKeys.PM46970_PostStripeCashApplicationTransactions"/> flag turned on, so webhook
/// tests can drive the non-charge cash application payment handlers (released dark behind the flag).
/// </summary>
public sealed class StripeCashApplicationFixture : StripeTestsFixture
{
    public BillingApplicationFactory Billing { get; }

    public StripeCashApplicationFixture()
    {
        Billing = new BillingApplicationFactory(Api.TestDatabase, new Dictionary<string, string?>
        {
            [$"Features:FlagValues:{FeatureFlagKeys.PM46970_PostStripeCashApplicationTransactions}"] = "true",
        });
    }

    public override async Task DisposeAsync()
    {
        await Billing.DisposeAsync();
        await base.DisposeAsync();
    }
}
