using Bit.Core.Platform.Installations;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Platform;

public static class PlatformServiceCollectionExtensions
{
    /// <summary>
    /// Extend DI to include commands and queries exported from the Platform
    /// domain.
    /// </summary>
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddScoped<IGetInstallationQuery, GetInstallationQuery>();
        services.AddScoped<IUpdateInstallationCommand, UpdateInstallationCommand>();

        return services;
    }
}
