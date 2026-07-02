namespace Bit.Billing.IntegrationTest;

/// <summary>
/// Variant of <see cref="StripeTestsFixture"/> that additionally spins up the
/// Bit.Billing webhook host sharing the API host's database, so webhook tests
/// can post Stripe events that reference real subscribers seeded through the
/// existing intent methods.
/// </summary>
public sealed class StripeWebhookTestsFixture : StripeTestsFixture
{
    public BillingApplicationFactory Billing { get; }

    public StripeWebhookTestsFixture()
    {
        Billing = new BillingApplicationFactory(Api.TestDatabase);
    }

    public override async ValueTask DisposeAsync()
    {
        await Billing.DisposeAsync();
        await base.DisposeAsync();
    }
}
