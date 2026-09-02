using Bit.Services.Pam.AccessConnector.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Api.Models.Request;

namespace Bit.Services.Pam.AccessConnector.Api.Endpoints;

/// <summary>
/// The <c>organizations/{orgId}/access-connectors</c> resource: fleet registration, enable/disable, deletion, and
/// target assignment. <c>orgId</c> is bound from the group's route prefix.
/// </summary>
internal static class AccessConnectorEndpoints
{
    public static RouteGroupBuilder MapAccessConnectorEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamAccessConnectors");

        group.MapGet("", (Guid orgId, AccessConnectorEndpointsHandler handler) => handler.GetAll(orgId))
            .WithName("Pam_AccessConnectors_GetAll");

        group.MapGet("{id:guid}", (Guid orgId, Guid id, AccessConnectorEndpointsHandler handler) => handler.Get(orgId, id))
            .WithName("Pam_AccessConnectors_Get")
            .WithDescription(
                "Returns one access connector with its recent rotation activity -- the jobs it has worked and " +
                "the attempts it recorded against them, newest first.");

        group.MapPost("", (Guid orgId, RegisterAccessConnectorRequestModel model, AccessConnectorEndpointsHandler handler) => handler.Post(orgId, model))
            .WithName("Pam_AccessConnectors_Post")
            .WithDescription(
                "Registers an access connector and returns its client secret. The secret is shown exactly once here " +
                "-- the server hashes it for storage and cannot return it again.");

        group.MapPost("{id:guid}/enable",
            async (Guid orgId, Guid id, AccessConnectorEndpointsHandler handler) =>
            {
                await handler.Enable(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Enable")
            .WithDescription("Re-enables a disabled access connector so it can authenticate and claim jobs again.");

        group.MapPost("{id:guid}/disable",
            async (Guid orgId, Guid id, AccessConnectorEndpointsHandler handler) =>
            {
                await handler.Disable(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Disable")
            .WithDescription(
                "Disables an access connector (reversible): it stops claiming new jobs and its running jobs " +
                "are released, but its credential is retained so it can be re-enabled later.");

        group.MapDelete("{id:guid}",
            async (Guid orgId, Guid id, AccessConnectorEndpointsHandler handler) =>
            {
                await handler.Delete(orgId, id);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Delete")
            .WithDescription(
                "Permanently deletes an access connector and invalidates its credential. The access " +
                "connector held the plaintext organization key -- rotating the organization key is the " +
                "remediation for a suspected compromise.");

        group.MapPost("{id:guid}/assignments",
            async (Guid orgId, Guid id, AssignAccessConnectorTargetRequestModel model, AccessConnectorEndpointsHandler handler) =>
            {
                await handler.AssignTarget(orgId, id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_AssignTarget");

        group.MapDelete("{id:guid}/assignments/{targetSystemId:guid}",
            async (Guid orgId, Guid id, Guid targetSystemId, AccessConnectorEndpointsHandler handler) =>
            {
                await handler.UnassignTarget(orgId, id, targetSystemId);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_UnassignTarget");

        return group;
    }
}
