using System.Security.Claims;
using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.Invoicing;
using Bit.Subscriptions.User.Handlers;
using Bit.Subscriptions.User.Models.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Bit.Subscriptions.User;

/// <summary>Maps the account-scoped subscription HTTP surface as a Minimal API endpoint group.</summary>
public static class UserSubscriptionEndpointsExtensions
{
    /// <summary>Attaches the account subscription group's shared cross-cutting chain to an empty group; the host owns the route prefix.</summary>
    public static RouteGroupBuilder MapUserSubscriptionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");
        group.WithTags("UserSubscriptions");
        group.WithGroupName("internal");
        group.RequireAuthorization(Policies.Application);
        group.WithBasicExceptionHandling();
        group.RequireFeature(InvoicingFeatureFlags.PM36631_PreviewDrivenCart);

        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        group.MapGet("upgrade/preview",
                async (ClaimsPrincipal principal, [AsParameters] GetSubscriptionUpgradePreviewRequest request,
                       [FromServices] GetAccountSubscriptionUpgradePreviewHandler handler) =>
                    await handler.HandleAsync(principal, request))
            .WithName("GetAccountSubscriptionUpgradePreview")
            .WithDescription("Previews the invoice for upgrading the user's Premium subscription to an organization plan.");

        group.MapGet("preview",
                async (ClaimsPrincipal principal, [FromServices] GetAccountSubscriptionPreviewHandler handler) =>
                    await handler.HandleAsync(principal))
            .WithName("GetAccountSubscriptionPreview")
            .WithDescription("Previews the account's upcoming subscription renewal.");

        group.MapGet("purchase/preview",
                async (ClaimsPrincipal principal, [AsParameters] GetSubscriptionPurchasePreviewRequest request,
                       [FromServices] GetAccountSubscriptionPurchasePreviewHandler handler) =>
                    await handler.HandleAsync(principal, request))
            .WithName("GetAccountSubscriptionPurchasePreview")
            .WithDescription("Previews the invoice for purchasing a Premium subscription.");

        group.MapPost("purchase/organization/preview",
                async (ClaimsPrincipal principal, [FromBody] GetOrganizationPurchasePreviewRequest request,
                       [FromServices] GetAccountOrganizationPurchasePreviewHandler handler) =>
                    await handler.HandleAsync(principal, request))
            .WithName("GetAccountOrganizationPurchasePreview")
            .WithDescription("Previews the invoice for purchasing an organization subscription.");

        return group;
    }
}
