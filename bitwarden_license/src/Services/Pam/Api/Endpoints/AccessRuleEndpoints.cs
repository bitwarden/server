using System.Security.Claims;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.OrganizationAuthorization;
using Bit.Services.Pam.Api.Authorization;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/access-rules</c> resource. The group requires <see cref="MemberRequirement"/> rather
/// than <c>MemberOrProviderRequirement</c>, since access rules gate who can lease credentials and are not a provider's
/// to read or change.
/// </summary>
internal static class AccessRuleEndpoints
{
    public static RouteGroupBuilder MapAccessRuleEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("AccessRules");
        group.RequireAuthorization(new AuthorizeAttribute<MemberRequirement>());

        group.MapGet("", ([FromRoute] Guid orgId, AccessRuleEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_AccessRules_GetAll");

        group.MapGet("{id:guid}", ([FromRoute] Guid orgId, Guid id, AccessRuleEndpointsHandler handler) => handler.Get(orgId, id))
            .WithName("Pam_AccessRules_Get");

        // Restricted to rule managers, since it reveals where a rule fails to protect credentials.
        group.MapGet("{id:guid}/bypassable-ciphers",
                ([FromRoute] Guid orgId, Guid id, AccessRuleEndpointsHandler handler) => handler.GetBypassableCiphers(orgId, id))
            .WithName("Pam_AccessRules_GetBypassableCiphers")
            .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        group.MapPost("", ([FromRoute] Guid orgId, AccessRuleRequestModel model, AccessRuleEndpointsHandler handler, ClaimsPrincipal user) => handler.Post(user, orgId, model))
            .WithName("Pam_AccessRules_Post")
            .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        group.MapPut("{id:guid}", ([FromRoute] Guid orgId, Guid id, AccessRuleRequestModel model, AccessRuleEndpointsHandler handler, ClaimsPrincipal user) => handler.Put(user, orgId, id, model))
            .WithName("Pam_AccessRules_Put")
            .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        group.MapDelete("{id:guid}",
            async ([FromRoute] Guid orgId, Guid id, AccessRuleEndpointsHandler handler, ClaimsPrincipal user) =>
            {
                await handler.Delete(user, orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessRules_Delete")
            .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        return group;
    }
}
