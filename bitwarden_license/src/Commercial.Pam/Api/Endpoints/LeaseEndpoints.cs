using System.Security.Claims;
using Bit.Commercial.Pam.Api.Endpoints.Handlers;
using Bit.Commercial.Pam.Api.Models.Request;

namespace Bit.Commercial.Pam.Api.Endpoints;

/// <summary>
/// The <c>leases</c> resource: the caller's own leases, the governance surface over manageable collections, and the
/// per-lease actions (revoke, extend). Mirrors the routes the former <c>LeasesController</c> served.
/// </summary>
internal static class LeaseEndpoints
{
    public static RouteGroupBuilder MapLeaseEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Leases");

        group.MapGet("active", (LeaseEndpointsHandler handler, ClaimsPrincipal user) => handler.GetActive(user))
            .WithName("Pam_Leases_GetActive");

        group.MapGet("history", (LeaseEndpointsHandler handler, ClaimsPrincipal user) => handler.GetHistory(user))
            .WithName("Pam_Leases_GetHistory");

        group.MapGet("mine", (LeaseEndpointsHandler handler, ClaimsPrincipal user) => handler.GetMine(user))
            .WithName("Pam_Leases_GetMine");

        group.MapPost("{id:guid}/revoke",
            async (Guid id, AccessLeaseRevokeRequestModel model, LeaseEndpointsHandler handler, ClaimsPrincipal user) =>
            {
                await handler.Revoke(user, id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Leases_Revoke");

        group.MapPost("{id:guid}/extend",
            (Guid id, AccessLeaseExtensionRequestModel model, LeaseEndpointsHandler handler, ClaimsPrincipal user) =>
                handler.Extend(user, id, model))
            .WithName("Pam_Leases_Extend");

        return group;
    }
}
