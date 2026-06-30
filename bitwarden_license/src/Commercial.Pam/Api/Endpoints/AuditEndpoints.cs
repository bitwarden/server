using System.Security.Claims;
using Bit.Commercial.Pam.Api.Endpoints.Handlers;

namespace Bit.Commercial.Pam.Api.Endpoints;

/// <summary>
/// The <c>audit</c> resource: the governance access-audit trail, synthesized over the caller's manageable collections.
/// A read-only projection of existing PAM state — no actions.
/// </summary>
internal static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("", (AuditEndpointsHandler handler, ClaimsPrincipal user) => handler.GetTrail(user))
            .WithName("Pam_Audit_GetTrail");

        return group;
    }
}
