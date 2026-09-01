using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/audit</c> resource: the org-wide access-audit trail, read from the
/// dedicated append-only audit store — read-only, no actions. Authorized by the AccessEventLogs permission: anyone who
/// can view the organization's event logs sees the full PAM audit trail, regardless of collection management.
/// </summary>
public class AuditEndpointsHandler(
    IFeatureService featureService,
    ICurrentContext currentContext,
    IListAccessAuditTrailQuery listAccessAuditTrailQuery,
    IListAccessAuditItemsQuery listAccessAuditItemsQuery)
{
    /// <summary>
    /// One page of the trail, newest first, narrowed by <paramref name="filter"/>. The response's continuation token
    /// is set while more pages remain and absent on the last one, matching the organization event log.
    /// </summary>
    public async Task<ListResponseModel<AccessAuditEventResponseModel>> GetTrail(
        Guid orgId, AccessAuditTrailFilterRequestModel filter)
    {
        // The kill switch stops the writes (see AccessAuditEventEmitter), so serving the trail while it is on would
        // hand an auditor a record that silently omits everything that happened since the flip. Withdrawing the
        // resource is the honest answer; the same 404 the permission check gives, so a caller learns nothing extra.
        if (featureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging))
        {
            throw new NotFoundException();
        }

        if (!await currentContext.AccessEventLogs(orgId))
        {
            throw new NotFoundException();
        }

        var page = await listAccessAuditTrailQuery.GetTrailAsync(orgId, filter.ToQueryOptions());
        return new ListResponseModel<AccessAuditEventResponseModel>(
            page.Data.Select(e => new AccessAuditEventResponseModel(e)),
            page.ContinuationToken);
    }

    /// <summary>
    /// The distinct subjects the trail names in <paramref name="range"/> — what the Item filter's menu is built from.
    ///
    /// Unpaged, and deliberately so: the result is one row per subject, bounded by how many credentials and rules the
    /// organization governs rather than by how much activity there has been. Guarded exactly as the trail is, because
    /// it describes the same records: the same kill switch and the same permission, so it cannot become a way to learn
    /// what the trail itself would not disclose.
    /// </summary>
    public async Task<ListResponseModel<AccessAuditItemResponseModel>> GetItems(
        Guid orgId, AccessAuditRangeRequestModel range)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging))
        {
            throw new NotFoundException();
        }

        if (!await currentContext.AccessEventLogs(orgId))
        {
            throw new NotFoundException();
        }

        var (start, end) = range.ToRange();
        var items = await listAccessAuditItemsQuery.GetItemsAsync(orgId, start, end);
        return new ListResponseModel<AccessAuditItemResponseModel>(
            items.Select(item => new AccessAuditItemResponseModel(item)));
    }
}
