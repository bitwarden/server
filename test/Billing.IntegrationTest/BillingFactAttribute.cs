namespace Bit.Billing.IntegrationTest;

/// <summary>
/// A fact that runs only when <c>RUN_STRIPE_INTEGRATION_TESTS</c> is set in the environment.
/// These tests hit a real Stripe account, so they are opt-in to avoid running in CI.
/// </summary>
public sealed class BillingFactAttribute : FactAttribute
{
    public BillingFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_STRIPE_INTEGRATION_TESTS") is null)
        {
            Skip = "Manual only — set RUN_STRIPE_INTEGRATION_TESTS to run.";
        }
    }
}
