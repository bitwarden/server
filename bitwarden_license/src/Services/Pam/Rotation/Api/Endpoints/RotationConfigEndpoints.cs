using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Api.Models.Request;

namespace Bit.Services.Pam.Rotation.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/rotation/configs</c> resource. <c>orgId</c> is bound from the group's route prefix.
/// </summary>
internal static class RotationConfigEndpoints
{
    public static RouteGroupBuilder MapRotationConfigEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamRotationConfigs");

        group.MapGet("", (Guid orgId, RotationConfigEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_Rotation_Configs_GetAll");

        group.MapGet("{id:guid}", (Guid orgId, Guid id, RotationConfigEndpointsHandler handler) => handler.Get(orgId, id))
            .WithName("Pam_Rotation_Configs_Get");

        group.MapPost("", (Guid orgId, CreateRotationConfigRequestModel model, RotationConfigEndpointsHandler handler) => handler.Post(orgId, model))
            .WithName("Pam_Rotation_Configs_Post");

        group.MapPut("{id:guid}/settings",
            (Guid orgId, Guid id, UpdateRotationSettingsRequestModel model, RotationConfigEndpointsHandler handler) =>
                handler.PutSettings(orgId, id, model))
            .WithName("Pam_Rotation_Configs_PutSettings");

        group.MapPut("{id:guid}/account",
            (Guid orgId, Guid id, UpdateRotationAccountRequestModel model, RotationConfigEndpointsHandler handler) =>
                handler.PutAccount(orgId, id, model))
            .WithName("Pam_Rotation_Configs_PutAccount");

        group.MapPost("{id:guid}/pause",
            async (Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.Pause(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Configs_Pause");

        group.MapPost("{id:guid}/resume",
            async (Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.Resume(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Configs_Resume");

        group.MapPost("{id:guid}/rotate",
            async (Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.Rotate(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Configs_Rotate")
            .WithDescription("Triggers an on-demand rotation now (spec TriggerRotationNow), subject to the per-config on-demand cooldown.");

        group.MapPost("{id:guid}/record-manual",
            async (Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.RecordManual(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Configs_RecordManual")
            .WithDescription("Records that an operator rotated a manual-target config's credential out of band, clearing its due obligation.");

        group.MapDelete("{id:guid}",
            async (Guid orgId, Guid id, RotationConfigEndpointsHandler handler) =>
            {
                await handler.Delete(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Configs_Delete");

        return group;
    }
}
