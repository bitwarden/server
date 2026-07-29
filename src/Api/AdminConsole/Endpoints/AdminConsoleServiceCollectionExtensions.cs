using Bit.Api.AdminConsole.Endpoints.Handlers;

namespace Bit.Api.AdminConsole.Endpoints;

/// <summary>
/// Registers the handler classes behind admin console Minimal API endpoints.
/// </summary>
public static class AdminConsoleServiceCollectionExtensions
{
    public static void AddAdminConsoleEndpointHandlers(this IServiceCollection services)
    {
        services.AddScoped<CollectionUserEndpointsHandler>();
    }
}
