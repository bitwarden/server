using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;
using Bit.OrganizationAuthorization;
using Bit.OrganizationKeys.Filters;
using Bit.OrganizationKeys.Handlers;
using Bit.OrganizationKeys.Models.Requests;
using Bit.OrganizationKeys.Requirements;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Bit.OrganizationKeys;

/// <summary>Maps the organization key HTTP surface as a Minimal API endpoint group.</summary>
public static class OrganizationKeysEndpointsExtensions
{
    /// <summary>
    /// Attaches the group's shared cross-cutting chain to an empty group; the host owns the route prefix. The prefix
    /// must contain an <c>{orgId}</c> route value, which the organization authorization requirements read.
    /// </summary>
    public static RouteGroupBuilder MapOrganizationKeysEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");
        group.WithTags("OrganizationKeys");
        group.WithGroupName("internal");
        group.RequireAuthorization(Policies.Application);
        group.WithBasicExceptionHandling();

        group.MapPost("organization-key-id",
                async ([FromRoute] Guid orgId, [FromBody] SetOrganizationKeyIdRequest request,
                       [FromServices] SetOrganizationKeyIdHandler handler) =>
                    await handler.HandleAsync(orgId, request))
            .AddEndpointFilter<RouteHandlerBuilder, RequestValidationEndpointFilter<SetOrganizationKeyIdRequest>>()
            .RequireAuthorization(new AuthorizeAttribute<OrganizationOwnerOrAdminRequirement>())
            .WithName("SetOrganizationKeyId")
            .WithDescription(
                "Reports the key id of the organization's current organization key, to backfill organizations " +
                "without one. Only Owners and Admins who hold the organization key can call this endpoint.")
            // The group's error metadata replaces the inferred success response, so document it explicitly.
            .Produces(StatusCodes.Status200OK);

        return group;
    }
}
