using Bit.OrganizationAuthorization;
using Bit.Services.Pam.Api.Authorization;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/audit</c> resource: the organization's read-only PAM access-audit trail, authorized by
/// <see cref="AccessAuditTrailRequirement"/>.
/// </summary>
internal static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Audit");
        group.RequireAuthorization(new AuthorizeAttribute<AccessAuditTrailRequirement>());

        group.MapGet("",
                (AuditEndpointsHandler handler, Guid orgId, [AsParameters] AccessAuditTrailFilterRequestModel filter) =>
                    handler.GetTrail(orgId, filter))
            .WithName("Pam_Audit_GetTrail");

        // The subjects the trail can be filtered by.
        group.MapGet("items",
                (AuditEndpointsHandler handler, Guid orgId, [AsParameters] AccessAuditRangeRequestModel range) =>
                    handler.GetItems(orgId, range))
            .WithName("Pam_Audit_GetItems");

        return group;
    }
}
