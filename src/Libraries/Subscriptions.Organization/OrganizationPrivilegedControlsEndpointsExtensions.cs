using Bit.Core;
using Bit.OrganizationAuthorization;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Requirements;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Bit.Subscriptions.Organization;

/// <summary>Maps the organization-scoped Privileged Controls subscription HTTP surface as a Minimal API endpoint group.</summary>
public static class OrganizationPrivilegedControlsEndpointsExtensions
{
    /// <summary>
    /// Maps the Privileged Controls endpoints on the shared organization-scoped chain, gated by
    /// <see cref="FeatureFlagKeys.PM39883_Pam_Billing_Support"/>. The host owns the route prefix.
    /// </summary>
    public static RouteGroupBuilder MapOrganizationPrivilegedControlsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapOrganizationScopedGroup();
        group.RequireFeature(FeatureFlagKeys.PM39883_Pam_Billing_Support);

        group.MapPut("privileged-controls",
                async ([FromRoute] Guid organizationId, [FromBody] PrivilegedControlsSeatsRequest request,
                    [FromServices] ChangePrivilegedControlsSeatsHandler handler) =>
                {
                    await handler.HandleAsync(organizationId, request);
                    return Results.NoContent();
                })
            .RequireAuthorization(new AuthorizeAttribute<StandaloneOrganizationOwnerRequirement>())
            .WithName("ChangePrivilegedControlsSeats")
            .WithDescription("Adds or removes the given number of Privileged Controls seats from the organization's current count and sets the autoscale limit.");

        return group;
    }
}
