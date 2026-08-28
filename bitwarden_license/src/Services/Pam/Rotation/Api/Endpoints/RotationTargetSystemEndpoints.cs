using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Api.Models.Request;

namespace Bit.Services.Pam.Rotation.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/rotation/target-systems</c> resource. <c>orgId</c> is bound from the group's route
/// prefix.
/// </summary>
internal static class RotationTargetSystemEndpoints
{
    public static RouteGroupBuilder MapRotationTargetSystemEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamRotationTargetSystems");

        group.MapGet("", (Guid orgId, RotationTargetSystemEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_Rotation_TargetSystems_GetAll");

        group.MapPost("", (Guid orgId, RegisterTargetSystemRequestModel model, RotationTargetSystemEndpointsHandler handler) => handler.Post(orgId, model))
            .WithName("Pam_Rotation_TargetSystems_Post");

        group.MapPost("{id:guid}/enable",
            async (Guid orgId, Guid id, RotationTargetSystemEndpointsHandler handler) =>
            {
                await handler.Enable(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_TargetSystems_Enable");

        group.MapPost("{id:guid}/disable",
            async (Guid orgId, Guid id, RotationTargetSystemEndpointsHandler handler) =>
            {
                await handler.Disable(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_TargetSystems_Disable");

        group.MapPut("{id:guid}/name",
            async (Guid orgId, Guid id, RenameTargetSystemRequestModel model, RotationTargetSystemEndpointsHandler handler) =>
            {
                await handler.Rename(orgId, id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_TargetSystems_Rename");

        group.MapPut("{id:guid}/policy",
            async (Guid orgId, Guid id, UpdateTargetSystemPolicyRequestModel model, RotationTargetSystemEndpointsHandler handler) =>
            {
                await handler.UpdatePolicy(orgId, id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_TargetSystems_UpdatePolicy");

        return group;
    }
}
