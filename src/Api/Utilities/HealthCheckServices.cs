using System.Diagnostics.CodeAnalysis;
using Bit.Core.Settings;

namespace Bit.Api.Utilities;

[ExcludeFromCodeCoverage]
internal static class HealthCheckServices
{
    public static IServiceCollection ConfigureHealthCheckServices(this IServiceCollection services,
        GlobalSettings globalSettings, IHostEnvironment environment)
    {
        services.AddHealthChecks();

        return services;
    }
}
