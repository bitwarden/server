using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Api.Models.Request;

namespace Bit.Services.Pam.Rotation.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/rotation/daemons</c> resource: fleet registration, revocation, and target
/// assignment. <c>orgId</c> is bound from the group's route prefix.
/// </summary>
internal static class RotationDaemonEndpoints
{
    public static RouteGroupBuilder MapRotationDaemonEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamRotationDaemons");

        group.MapGet("", (Guid orgId, RotationDaemonEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_Rotation_Daemons_GetAll");

        group.MapPost("", (Guid orgId, RegisterDaemonRequestModel model, RotationDaemonEndpointsHandler handler) => handler.Post(orgId, model))
            .WithName("Pam_Rotation_Daemons_Post")
            .WithDescription(
                "Registers a rotation daemon and returns its client secret. The secret is shown exactly once here " +
                "-- the server hashes it for storage and cannot return it again.");

        group.MapPost("{id:guid}/revoke",
            async (Guid orgId, Guid id, RotationDaemonEndpointsHandler handler) =>
            {
                await handler.Revoke(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Daemons_Revoke")
            .WithDescription(
                "Revokes the daemon's credential. A revoked daemon has held the plaintext organization key -- " +
                "rotating the organization key is the remediation for a suspected compromise.");

        group.MapPost("{id:guid}/assignments",
            async (Guid orgId, Guid id, AssignDaemonTargetRequestModel model, RotationDaemonEndpointsHandler handler) =>
            {
                await handler.AssignTarget(orgId, id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Daemons_AssignTarget");

        group.MapDelete("{id:guid}/assignments/{targetSystemId:guid}",
            async (Guid orgId, Guid id, Guid targetSystemId, RotationDaemonEndpointsHandler handler) =>
            {
                await handler.UnassignTarget(orgId, id, targetSystemId);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Daemons_UnassignTarget");

        return group;
    }
}
