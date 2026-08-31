using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Services.Pam.Api.Authorization;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/access-rules</c> resource: rule CRUD scoped to an organization. <c>orgId</c> is
/// bound from the group's route prefix.
/// </summary>
/// <remarks>
/// Authorization runs in the middleware rather than the handler. The group requires organization membership and the
/// write endpoints additionally require <see cref="ManageAccessRulesRequirement"/>; ASP.NET combines the group and
/// endpoint policies, so a write has to satisfy both. Both requirements read <c>orgId</c> off the route, which the
/// group prefix supplies.
/// <para>
/// The group requirement is deliberately <see cref="MemberRequirement"/> and not
/// <c>MemberOrProviderRequirement</c>: providers manage an organization's billing and configuration, but access
/// rules gate who can lease credentials out of it, which is not theirs to read or change.
/// <see cref="ManageAccessRulesRequirement"/> excludes providers on its own, so neither gate depends on the other
/// to keep them out.
/// </para>
/// </remarks>
internal static class AccessRuleEndpoints
{
    public static RouteGroupBuilder MapAccessRuleEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("AccessRules");
        group.RequireAuthorization(new AuthorizeAttribute<MemberRequirement>());

        group.MapGet("", (Guid orgId, AccessRuleEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_AccessRules_GetAll");

        group.MapGet("{id:guid}", (Guid orgId, Guid id, AccessRuleEndpointsHandler handler) => handler.Get(orgId, id))
            .WithName("Pam_AccessRules_Get");

        // Diagnostic, and admin-only: it names credentials a rule is failing to protect, which is the
        // rules admin's business and not every member's. The group's MemberRequirement alone would be
        // too weak, so this read carries the write endpoints' requirement even though it mutates nothing.
        group.MapGet("{id:guid}/bypassable-ciphers",
                (Guid orgId, Guid id, AccessRuleEndpointsHandler handler) => handler.GetBypassableCiphers(orgId, id))
            .WithName("Pam_AccessRules_GetBypassableCiphers")
            .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        group.MapPost("", (Guid orgId, AccessRuleRequestModel model, AccessRuleEndpointsHandler handler) => handler.Post(orgId, model))
            .WithName("Pam_AccessRules_Post")
            .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        group.MapPut("{id:guid}", (Guid orgId, Guid id, AccessRuleRequestModel model, AccessRuleEndpointsHandler handler) => handler.Put(orgId, id, model))
            .WithName("Pam_AccessRules_Put")
            .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        group.MapDelete("{id:guid}",
            async (Guid orgId, Guid id, AccessRuleEndpointsHandler handler) =>
            {
                await handler.Delete(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessRules_Delete")
            .RequireAuthorization(new AuthorizeAttribute<ManageAccessRulesRequirement>());

        return group;
    }
}
