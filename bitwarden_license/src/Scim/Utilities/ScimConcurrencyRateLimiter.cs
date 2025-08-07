using System.Threading.RateLimiting;
using Bit.Core.AdminConsole.Authorization;

namespace Bit.Scim.Utilities;

public static class ScimConcurrencyRateLimiter
{
    public const string PolicyName = "ScimConcurrencyLimiter";

    public static IServiceCollection AddScimConcurrencyRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options => options.AddPolicy(PolicyName, context =>
            RateLimitPartition.GetConcurrencyLimiter(context.GetOrganizationId(),
                factory: _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = int.MaxValue, // How much do we want in memory
                    QueueLimit = 1,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                })
        ));
}
