using System.Security.Claims;
using Bit.OrganizationAuthorization;
using Bit.Services.Pam.Api.Authorization;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/access-rules</c> resource. Every route, reads included, requires
/// <see cref="ManageAccessRulesRequirement"/>, which also keeps providers out. Members learn whether a collection
/// is gated from its <c>HasEnabledAccessRule</c> flag instead.
/// </summary>
internal static class AccessRuleEndpoints
{
    public static RouteGroupBuilder MapAccessRuleEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("AccessRules");
        group.RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        group.MapGet("", ([FromRoute] Guid orgId, AccessRuleEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_AccessRules_GetAll");

        group.MapGet("{id:guid}", ([FromRoute] Guid orgId, Guid id, AccessRuleEndpointsHandler handler) => handler.Get(orgId, id))
            .WithName("Pam_AccessRules_Get");

        group.MapPost("", ([FromRoute] Guid orgId, AccessRuleRequestModel model, AccessRuleEndpointsHandler handler, ClaimsPrincipal user) => handler.Post(user, orgId, model))
            .WithName("Pam_AccessRules_Post");

        group.MapPut("{id:guid}", ([FromRoute] Guid orgId, Guid id, AccessRuleRequestModel model, AccessRuleEndpointsHandler handler, ClaimsPrincipal user) => handler.Put(user, orgId, id, model))
            .WithName("Pam_AccessRules_Put");

        group.MapDelete("{id:guid}",
            async ([FromRoute] Guid orgId, Guid id, AccessRuleEndpointsHandler handler, ClaimsPrincipal user) =>
            {
                await handler.Delete(user, orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessRules_Delete");

        return group;
    }
}
