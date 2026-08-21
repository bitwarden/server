using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.Invoicing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Subscriptions.Organization;

/// <summary>Maps the organization-scoped subscription HTTP surface as a Minimal API endpoint group.</summary>
public static class OrganizationSubscriptionEndpointsExtensions
{
    /// <summary>
    /// Attaches the organization subscription group's shared cross-cutting chain to an empty group;
    /// the host owns the route prefix. Only an authenticated caller is required here; each handler
    /// performs its own organization billing authorization check.
    /// </summary>
    public static RouteGroupBuilder MapOrganizationSubscriptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");
        group.WithTags("OrganizationSubscriptions");
        group.WithGroupName("internal");
        group.RequireAuthorization(Policies.Application);
        group.WithBasicExceptionHandling();
        group.RequireFeature(InvoicingFeatureFlags.PM36631_PreviewDrivenCart);
        return group;
    }
}
