using Microsoft.Extensions.Diagnostics.HealthChecks;
using Stripe;

namespace Bit.Billing.HealthChecks;

public class StripeHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
    {
        try
        {
            var accountService = new AccountService();
            _ = await accountService.ListAsync(
                new AccountListOptions { Limit = 1 },
                null,
                cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (StripeException)
        {
            return HealthCheckResult.Unhealthy("Stripe is down.");
        }
    }
}
