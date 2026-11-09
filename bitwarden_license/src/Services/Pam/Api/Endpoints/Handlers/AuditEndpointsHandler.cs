using Bit.Core;
using Bit.Core.Exceptions;
using Bit.HttpExtensions;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the <c>organizations/{orgId}/audit</c> resource. Authorization runs in the middleware (see
/// <c>AuditEndpoints</c>).
/// </summary>
public class AuditEndpointsHandler(
    IFeatureService featureService,
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

        var page = await listAccessAuditTrailQuery.GetTrailAsync(orgId, filter.ToQueryOptions());
        return new ListResponseModel<AccessAuditEventResponseModel>(
            page.Data.Select(e => new AccessAuditEventResponseModel(e)),
            page.ContinuationToken);
    }

    /// <summary>
    /// The distinct subjects the trail names in <paramref name="range"/>, for the Item filter. Unpaged, and withdrawn
    /// with the trail by the kill switch.
    /// </summary>
    public async Task<ListResponseModel<AccessAuditItemResponseModel>> GetItems(
        Guid orgId, AccessAuditRangeRequestModel range)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PamDisableSqlAuditLogging))
        {
            throw new NotFoundException();
        }

        var (start, end) = range.ToRange();
        var items = await listAccessAuditItemsQuery.GetItemsAsync(orgId, start, end);
        return new ListResponseModel<AccessAuditItemResponseModel>(
            items.Select(item => new AccessAuditItemResponseModel(item)));
    }
}
