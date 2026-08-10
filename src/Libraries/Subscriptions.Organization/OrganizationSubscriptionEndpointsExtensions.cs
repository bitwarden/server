using Bit.Core;
using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Subscriptions.Organization;

/// <summary>Maps the organization-scoped subscription HTTP surface as a Minimal API endpoint group.</summary>
public static class OrganizationSubscriptionEndpointsExtensions
{
    /// <summary>Attaches the organization subscription group and its shared chain. Empty at this stage. Only an authenticated caller is required here; each handler performs its own organization billing authorization check.</summary>
    public static RouteGroupBuilder MapOrganizationSubscriptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/organizations/{organizationId:guid}/billing");
        group.WithTags("OrganizationSubscriptions");
        group.RequireAuthorization(Policies.Application);
        group.RequireFeature(FeatureFlagKeys.PM36631_PreviewDrivenCart);
        return group;
    }
}
