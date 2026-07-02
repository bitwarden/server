using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/audit</c> resource: the synthesized, org-wide access-audit trail. A
/// read-only projection of existing PAM state — no actions. Authorized by the AccessEventLogs permission: anyone who
/// can view the organization's event logs sees the full PAM audit trail, regardless of collection management.
/// </summary>
public class AuditEndpointsHandler(
    ICurrentContext currentContext,
    IListAccessAuditTrailQuery listAccessAuditTrailQuery)
{
    public async Task<ListResponseModel<AccessAuditEventResponseModel>> GetTrail(Guid orgId)
    {
        if (!await currentContext.AccessEventLogs(orgId))
        {
            throw new NotFoundException();
        }

        var events = await listAccessAuditTrailQuery.GetTrailAsync(orgId);
        return new ListResponseModel<AccessAuditEventResponseModel>(
            events.Select(e => new AccessAuditEventResponseModel(e)));
    }
}
