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
        endpoints.MapCollectionEndpoints();
    }

    /// <summary>
    /// Applies the shared admin console endpoint chain to a single Minimal API endpoint. Keeps
    /// <see cref="AdminConsoleExceptionHandlerEndpointFilter"/> outermost so exceptions from downstream
    /// filters and the handler are translated into the <c>ErrorResponseModel</c> contract, and hides
    /// the endpoint from the public OpenAPI spec via the "internal" group name.
    /// </summary>
    public static RouteHandlerBuilder WithAdminConsoleDefaults(this RouteHandlerBuilder builder)
    {
        builder.RequireAuthorization(Policies.Application);
        builder.AddEndpointFilter<AdminConsoleExceptionHandlerEndpointFilter>();
        builder.WithGroupName("internal");
        return builder;
    }
}
