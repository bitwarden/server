using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.Invoicing;
using Bit.OrganizationAuthorization;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
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
    /// Every endpoint inherits the group baseline (<see cref="Policies.Application"/> +
    /// <see cref="OrganizationBillingRequirement"/>), so handlers don't repeat that check; an endpoint may
    /// then narrow it.
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

        // Previews are per-organization billing data; keep them out of any shared or browser cache.
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        group.MapGet("preview",
                async ([FromRoute] Guid organizationId, [FromServices] GetOrganizationSubscriptionPreviewHandler handler) => await handler.HandleAsync(organizationId))
            .RequireAuthorization(new AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>())
            .WithName("GetOrganizationSubscriptionPreview")
            .WithDescription("Previews the organization's upcoming subscription renewal.");

        group.MapGet("plan-change/preview",
                async ([FromRoute] Guid organizationId, [AsParameters] GetOrganizationPlanChangePreviewRequest previewRequest,
                        [FromServices] GetOrganizationPlanChangePreviewHandler handler) => await handler.HandleAsync(organizationId, previewRequest))
            .RequireAuthorization(new AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>())
            .WithName("GetOrganizationPlanChangePreview")
            .WithDescription("Previews the cost of changing the organization's plan.");

        return group;
    }
}
