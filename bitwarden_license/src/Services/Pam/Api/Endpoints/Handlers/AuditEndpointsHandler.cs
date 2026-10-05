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
/// Handler for the <c>organizations/{orgId}/audit</c> resource. Authorized by the AccessEventLogs permission, which
/// grants the full organization-wide trail.
/// </summary>
public class AuditEndpointsHandler(
    IFeatureService featureService,
    ICurrentContext currentContext,
    IListAccessAuditTrailQuery listAccessAuditTrailQuery,
    IListAccessAuditItemsQuery listAccessAuditItemsQuery)
{
    /// <summary>
    /// One page of the trail, newest first, narrowed by <paramref name="filter"/>. The continuation token is set
    /// while more pages remain.
    /// </summary>
    public async Task<ListResponseModel<AccessAuditEventResponseModel>> GetTrail(
        Guid orgId, AccessAuditTrailFilterRequestModel filter)
    {
        // The trail is incomplete while the kill switch is on, so it is withdrawn.
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
    /// The distinct subjects the trail names in <paramref name="range"/>, for the Item filter. Unpaged, and
    /// guarded exactly as the trail is.
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
