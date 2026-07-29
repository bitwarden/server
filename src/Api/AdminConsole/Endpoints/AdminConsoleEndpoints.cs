using Bit.Api.AdminConsole.Endpoints.Filters;
using Bit.Core.Auth.Identity;

namespace Bit.Api.AdminConsole.Endpoints;

/// <summary>
/// Maps the admin console Minimal API endpoint groups. Add new groups here rather than in <c>Startup.cs</c>.
/// </summary>
public static class AdminConsoleEndpoints
{
    public static void MapAdminConsoleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCollectionUserEndpoints();
    }

    public static RouteGroupBuilder WithAdminConsoleDefaults(this RouteGroupBuilder group)
    {
        group.RequireAuthorization(Policies.Application);
        group.AddEndpointFilter<AdminConsoleExceptionHandlerEndpointFilter>();
        group.WithGroupName("internal");
        return group;
    }
}
