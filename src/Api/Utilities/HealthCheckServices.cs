using System.Diagnostics.CodeAnalysis;

namespace Bit.Api.Utilities;

[ExcludeFromCodeCoverage]
internal static class HealthCheckServices
{
    public static IServiceCollection ConfigureHealthCheckServices(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHealthChecks();

        return services;
    }
}
