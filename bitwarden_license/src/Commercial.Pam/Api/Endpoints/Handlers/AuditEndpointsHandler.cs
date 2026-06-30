using System.Security.Claims;
using Bit.Commercial.Pam.Api.Models.Response;
using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.HttpExtensions;

namespace Bit.Commercial.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>audit</c> resource: the synthesized access-audit trail over the caller's manageable collections.
/// Thin -- the Minimal API endpoint (see <c>AuditEndpoints</c>) resolves this from DI.
/// </summary>
public class AuditEndpointsHandler(
    IUserService userService,
    IListAccessAuditTrailQuery listAccessAuditTrailQuery)
{
    public async Task<ListResponseModel<AccessAuditEventResponseModel>> GetTrail(ClaimsPrincipal user)
    {
        var userId = userService.GetProperUserId(user)!.Value;
        var events = await listAccessAuditTrailQuery.GetTrailAsync(userId);
        return new ListResponseModel<AccessAuditEventResponseModel>(
            events.Select(e => new AccessAuditEventResponseModel(e)));
    }
}
