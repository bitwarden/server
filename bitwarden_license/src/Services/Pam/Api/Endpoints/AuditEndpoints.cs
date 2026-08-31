using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/audit</c> resource: the org-wide governance access-audit trail, authorized by the
/// AccessEventLogs permission. A read-only projection of existing PAM state — no actions.
/// </summary>
internal static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Audit");

        group.MapGet("",
                (AuditEndpointsHandler handler, Guid orgId, [AsParameters] AccessAuditTrailFilterRequestModel filter) =>
                    handler.GetTrail(orgId, filter))
            .WithName("Pam_Audit_GetTrail");

        // A sibling of the trail rather than a shape of it: same resource and same authorization, but it answers what
        // the trail could be filtered BY, not what it holds.
        group.MapGet("items",
                (AuditEndpointsHandler handler, Guid orgId, [AsParameters] AccessAuditRangeRequestModel range) =>
                    handler.GetItems(orgId, range))
            .WithName("Pam_Audit_GetItems");

        return group;
    }
}
