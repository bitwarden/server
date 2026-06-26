using System.Security.Claims;
using Bit.Commercial.Pam.Api.Endpoints.Handlers;
using Bit.Commercial.Pam.Api.Models.Request;

namespace Bit.Commercial.Pam.Api.Endpoints;

/// <summary>
/// The <c>access-requests</c> resource: lease requests through their lifecycle (the requester's own queue plus the
/// approver's queue and decision). Mirrors the routes the former <c>AccessRequestsController</c> served.
/// </summary>
internal static class AccessRequestEndpoints
{
    public static RouteGroupBuilder MapAccessRequestEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("AccessRequests");

        group.MapGet("inbox", (AccessRequestEndpointsHandler handler, ClaimsPrincipal user) => handler.GetInbox(user))
            .WithName("Pam_AccessRequests_GetInbox");

        group.MapGet("history", (AccessRequestEndpointsHandler handler, ClaimsPrincipal user) => handler.GetHistory(user))
            .WithName("Pam_AccessRequests_GetHistory");

        group.MapGet("mine", (AccessRequestEndpointsHandler handler, ClaimsPrincipal user) => handler.GetMine(user))
            .WithName("Pam_AccessRequests_GetMine");

        group.MapGet("{id:guid}",
            (Guid id, AccessRequestEndpointsHandler handler, ClaimsPrincipal user) => handler.GetDetails(user, id))
            .WithName("Pam_AccessRequests_GetDetails");

        group.MapPost("{id:guid}/decision",
            (Guid id, AccessDecisionRequestModel model, AccessRequestEndpointsHandler handler, ClaimsPrincipal user) =>
                handler.Decide(user, id, model))
            .WithName("Pam_AccessRequests_Decide");

        group.MapPost("{id:guid}/activate",
            (Guid id, AccessRequestEndpointsHandler handler, ClaimsPrincipal user) => handler.Activate(user, id))
            .WithName("Pam_AccessRequests_Activate");

        group.MapPost("{id:guid}/revoke",
            async (Guid id, AccessRequestEndpointsHandler handler, ClaimsPrincipal user) =>
            {
                await handler.Revoke(user, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessRequests_Revoke");

        return group;
    }
}
