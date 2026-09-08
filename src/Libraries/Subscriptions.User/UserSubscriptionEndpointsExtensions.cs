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

        // The host mounts this group at /account/billing/subscription/premium.
        group.MapPost("upgrade/invoice/preview",
                async (ClaimsPrincipal principal,
                        PreviewInvoiceForPremiumOrgUpgradeRequest request,
                        [FromServices] UserSubscriptionEndpointsHandler handler) =>
                    TypedResults.Ok(await handler.PreviewPremiumOrgUpgradeAsync(principal, request)))
            .WithName("PreviewPremiumOrgUpgradeInvoice")
            .WithDescription("Previews the invoice for upgrading a Premium subscription to an organization plan.");

        return group;
    }
}
