using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/access-connectors/rotation/target-systems</c> resource. <c>orgId</c> is bound from the group's
/// route prefix.
/// </summary>
internal static class TargetSystemEndpoints
{
    public static RouteGroupBuilder MapTargetSystemEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamAccessConnectorRotationTargetSystems");

        group.MapGet("", ([FromRoute] Guid orgId, TargetSystemEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_AccessConnectors_Rotation_TargetSystems_GetAll");

        group.MapPost("", ([FromRoute] Guid orgId, RegisterTargetSystemRequestModel model, TargetSystemEndpointsHandler handler) => handler.Post(orgId, model))
            .WithName("Pam_AccessConnectors_Rotation_TargetSystems_Post");

        group.MapPost("{id:guid}/enable",
            async ([FromRoute] Guid orgId, Guid id, TargetSystemEndpointsHandler handler) =>
            {
                await handler.Enable(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_TargetSystems_Enable");

        group.MapPost("{id:guid}/disable",
            async ([FromRoute] Guid orgId, Guid id, TargetSystemEndpointsHandler handler) =>
            {
                await handler.Disable(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_TargetSystems_Disable");

        group.MapPut("{id:guid}",
            async ([FromRoute] Guid orgId, Guid id, UpdateTargetSystemRequestModel model, TargetSystemEndpointsHandler handler) =>
            {
                await handler.Put(orgId, id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_TargetSystems_Put");

        group.MapDelete("{id:guid}",
            async ([FromRoute] Guid orgId, Guid id, TargetSystemEndpointsHandler handler) =>
            {
                await handler.Delete(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_TargetSystems_Delete");

        return group;
    }
}
