using Bit.Api.AdminConsole.Endpoints.Filters;
using Bit.Core.Auth.Identity;

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

    /// <summary>
    /// Applies the parts of the endpoint chain that are the same for every admin console Minimal API group:
    /// the application authorization policy and the shared exception filter. Feature-flag gating stays a
    /// separate per-group call, since each admin console feature has its own flag.
    /// </summary>
    public static RouteGroupBuilder WithAdminConsoleDefaults(this RouteGroupBuilder group)
    {
        group.RequireAuthorization(Policies.Application);
        group.AddEndpointFilter<AdminConsoleExceptionHandlerEndpointFilter>();
        return group;
    }
}
