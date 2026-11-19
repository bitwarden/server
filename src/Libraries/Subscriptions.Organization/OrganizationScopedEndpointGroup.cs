using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.OrganizationAuthorization;
using Bit.Subscriptions.Organization.Requirements;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bit.Subscriptions.Organization;

internal static class OrganizationScopedEndpointGroup
{
    /// <summary>The baseline every organization-scoped group shares; callers add their own feature gate.</summary>
    public static RouteGroupBuilder MapOrganizationScopedGroup(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");
        group.WithTags("OrganizationSubscriptions");
        group.WithGroupName("internal");
        group.RequireAuthorization(Policies.Application);
        group.RequireAuthorization(new AuthorizeAttribute<OrganizationBillingRequirement>());
        group.WithBasicExceptionHandling();

        // Responses are per-organization billing data; keep them out of any shared or browser cache.
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        return group;
    }
}
