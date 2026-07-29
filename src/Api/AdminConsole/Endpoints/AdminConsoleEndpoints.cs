namespace Bit.Api.AdminConsole.Endpoints;

/// <summary>
/// Maps every admin console Minimal API endpoint group. New groups get added here instead of in <c>Startup.cs</c>.
/// </summary>
public static class AdminConsoleEndpoints
{
    public static void MapAdminConsoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCollectionUserEndpoints();
    }
}
