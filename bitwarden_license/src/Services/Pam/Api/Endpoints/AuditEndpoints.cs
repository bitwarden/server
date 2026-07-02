using Bit.Services.Pam.Api.Endpoints.Handlers;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/audit</c> resource: the org-wide governance access-audit trail, authorized by the
/// AccessEventLogs permission. A read-only projection of existing PAM state — no actions.
/// </summary>
internal static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("", (AuditEndpointsHandler handler, Guid orgId) => handler.GetTrail(orgId))
            .WithName("Pam_Audit_GetTrail");

        return group;
    }
}
