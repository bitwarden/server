using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.Rotation.Api.Models.Request;

namespace Bit.Services.Pam.Rotation.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/rotation/daemons</c> resource: fleet registration, enable/disable, deletion, and
/// target assignment. <c>orgId</c> is bound from the group's route prefix.
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

        group.MapPost("{id:guid}/enable",
            async (Guid orgId, Guid id, RotationDaemonEndpointsHandler handler) =>
            {
                await handler.Enable(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Daemons_Enable")
            .WithDescription("Re-enables a disabled daemon so it can authenticate and claim jobs again.");

        group.MapPost("{id:guid}/disable",
            async (Guid orgId, Guid id, RotationDaemonEndpointsHandler handler) =>
            {
                await handler.Disable(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Daemons_Disable")
            .WithDescription(
                "Disables a daemon (reversible): it stops claiming new jobs and its running jobs are released, but " +
                "its credential is retained so it can be re-enabled later.");

        group.MapDelete("{id:guid}",
            async (Guid orgId, Guid id, RotationDaemonEndpointsHandler handler) =>
            {
                await handler.Delete(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_Rotation_Daemons_Delete")
            .WithDescription(
                "Permanently deletes a daemon and invalidates its credential. The daemon held the plaintext " +
                "organization key -- rotating the organization key is the remediation for a suspected compromise.");

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
