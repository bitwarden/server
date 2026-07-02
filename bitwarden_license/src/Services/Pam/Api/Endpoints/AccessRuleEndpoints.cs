using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;

namespace Bit.Services.Pam.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/access-rules</c> resource: rule CRUD scoped to an organization. Mirrors the routes
/// the former <c>AccessRulesController</c> served. <c>orgId</c> is bound from the group's route prefix.
/// </summary>
internal static class AccessRuleEndpoints
{
    public static RouteGroupBuilder MapAccessRuleEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("AccessRules");

        group.MapGet("", (Guid orgId, AccessRuleEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_AccessRules_GetAll");

        group.MapGet("{id:guid}", (Guid orgId, Guid id, AccessRuleEndpointsHandler handler) => handler.Get(orgId, id))
            .WithName("Pam_AccessRules_Get");

        group.MapPost("", (Guid orgId, AccessRuleRequestModel model, AccessRuleEndpointsHandler handler) => handler.Post(orgId, model))
            .WithName("Pam_AccessRules_Post");

        group.MapPut("{id:guid}", (Guid orgId, Guid id, AccessRuleRequestModel model, AccessRuleEndpointsHandler handler) => handler.Put(orgId, id, model))
            .WithName("Pam_AccessRules_Put");

        group.MapDelete("{id:guid}",
            async (Guid orgId, Guid id, AccessRuleEndpointsHandler handler) =>
            {
                await handler.Delete(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessRules_Delete");

        return group;
    }
}
