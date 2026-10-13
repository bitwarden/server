using System.Security.Claims;
using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.Invoicing;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Bit.Subscriptions.Organization;

/// <summary>Maps the organization subscription purchase HTTP surface as a Minimal API endpoint group.</summary>
public static class OrganizationSubscriptionPurchaseEndpointsExtensions
{
    /// <summary>
    /// Attaches the purchase group's cross-cutting chain to an empty group; the host owns the route prefix.
    /// The caller has no organization yet, so the group requires only <see cref="Policies.Application"/>.
    /// </summary>
    public static RouteGroupBuilder MapOrganizationSubscriptionPurchaseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");
        group.WithTags("OrganizationSubscriptions");
        group.WithGroupName("internal");
        group.RequireAuthorization(Policies.Application);
        group.WithBasicExceptionHandling();
        group.RequireFeature(InvoicingFeatureFlags.PM36631_PreviewDrivenCart);

        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        group.MapPost("purchase/preview",
                async (ClaimsPrincipal principal, [FromBody] PreviewOrganizationSubscriptionPurchaseRequest request,
                       [FromServices] PreviewOrganizationSubscriptionPurchaseHandler handler) =>
                    await handler.HandleAsync(principal, request))
            .WithName("PreviewOrganizationSubscriptionPurchase")
            .WithDescription("Previews the invoice for purchasing an organization subscription.");

        return group;
    }
}
