using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints;

/// <summary>The connector-facing <c>access-connectors/rotation/attempts</c> resource; <c>Bit.Api</c> maps the cipher routes.</summary>
internal static class RotationAttemptEndpoints
{
    public static RouteGroupBuilder MapRotationAttemptEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamAccessConnectorRotationAttempts");

        group.MapPost("{id:guid}/success",
            async (Guid id, ReportRotationSucceededRequestModel model, RotationAttemptEndpointsHandler handler) =>
            {
                await handler.Success(id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_Attempts_Success")
            .WithDescription(
                "Reports a successful rotation (spec RecordRotationSucceeded). Requires the attempt to already " +
                "have a written cipher (the VerifiedBeforeSuccess backstop); otherwise the report is treated as " +
                "stale (409, audited as report_rejected).");

        group.MapPost("{id:guid}/failure",
            async (Guid id, ReportRotationFailedRequestModel model, RotationAttemptEndpointsHandler handler) =>
            {
                await handler.Failure(id, model);
                return TypedResults.NoContent();
            })
            .WithName("Pam_AccessConnectors_Rotation_Attempts_Failure")
            .WithDescription(
                "Reports a failed rotation attempt (spec RecordRotationFailed). Never forward raw target-system " +
                "error output as ErrorCode/Detail -- it can echo credentials. Send a bounded error code plus an " +
                "optional short detail instead. Either field exceeding its documented length is rejected as a 400, " +
                "so cap them before sending -- a rejected report leaves the failure unrecorded until the job " +
                "times out. The single reason the two combine into is truncated rather than rejected.");

        return group;
    }
}
