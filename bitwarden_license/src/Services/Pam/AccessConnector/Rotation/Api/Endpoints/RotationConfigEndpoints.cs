using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/access-connectors/rotation/configs</c> resource. <c>orgId</c> is bound from the group's
/// route prefix.
/// </summary>
internal static class RotationConfigEndpoints
{
    public static RouteGroupBuilder MapRotationConfigEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamAccessConnectorRotationConfigs");

        group.MapGet("", ([FromRoute] Guid orgId, RotationConfigEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_AccessConnectors_Rotation_Configs_GetAll");

        group.MapGet("{id:guid}", ([FromRoute] Guid orgId, Guid id, RotationConfigEndpointsHandler handler) => handler.Get(orgId, id))
            .WithName("Pam_AccessConnectors_Rotation_Configs_Get");

        group.MapPost("", ([FromRoute] Guid orgId, CreateRotationConfigRequestModel model, RotationConfigEndpointsHandler handler) => handler.Post(orgId, model))
            .WithName("Pam_AccessConnectors_Rotation_Configs_Post");

        group.MapPut("{id:guid}",
            ([FromRoute] Guid orgId, Guid id, UpdateRotationConfigRequestModel model, RotationConfigEndpointsHandler handler) =>
                handler.Put(orgId, id, model))
            .WithName("Pam_AccessConnectors_Rotation_Configs_Put");

        group.MapPost("{id:guid}/pause",
            async ([FromRoute] Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.Pause(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_Configs_Pause");

        group.MapPost("{id:guid}/resume",
            async ([FromRoute] Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.Resume(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_Configs_Resume");

        group.MapPost("{id:guid}/rotate",
            async ([FromRoute] Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.Rotate(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_Configs_Rotate")
            .WithDescription("Triggers an on-demand rotation now (spec TriggerRotationNow), subject to the per-config on-demand cooldown.");

        group.MapPost("{id:guid}/record-manual",
            async ([FromRoute] Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.RecordManual(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_Configs_RecordManual")
            .WithDescription("Records that an operator rotated a manual-target config's credential out of band, clearing its due obligation.");

        group.MapDelete("{id:guid}",
            async ([FromRoute] Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.Delete(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_Configs_Delete");

        return group;
    }
}
