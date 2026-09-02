using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.Invoicing;
using Bit.OrganizationAuthorization;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Requirements;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Bit.Subscriptions.Organization;

/// <summary>Maps the organization-scoped subscription HTTP surface as a Minimal API endpoint group.</summary>
public static class OrganizationSubscriptionEndpointsExtensions
{
    /// <summary>
    /// Attaches the group's shared cross-cutting chain to an empty group; the host owns the route prefix.
    /// The group authorizes every endpoint (<see cref="Policies.Application"/> + <see cref="OrganizationBillingRequirement"/>),
    /// so handlers don't repeat the access check.
    /// </summary>
    public static RouteGroupBuilder MapOrganizationSubscriptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");
        group.WithTags("OrganizationSubscriptions");
        group.WithGroupName("internal");
        group.RequireAuthorization(Policies.Application);
        group.RequireAuthorization(new AuthorizeAttribute<OrganizationBillingRequirement>());
        group.WithBasicExceptionHandling();
        group.RequireFeature(InvoicingFeatureFlags.PM36631_PreviewDrivenCart);

        group.MapGet("preview",
                async (Guid organizationId, [FromServices] OrganizationSubscriptionEndpointsHandler handler) => await handler.GetPreviewAsync(organizationId))
            .WithName("GetOrganizationSubscriptionPreview")
            .WithDescription("Previews the organization's upcoming subscription renewal.");

        return group;
    }
}
