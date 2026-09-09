using Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints;

/// <summary>The connector-facing <c>access-connectors/rotation/jobs</c> resource: the poll and the claim.</summary>
internal static class RotationJobEndpoints
{
    public static RouteGroupBuilder MapRotationJobEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamAccessConnectorRotationJobs");

        group.MapGet("", (RotationJobEndpointsHandler handler) => handler.GetJobs())
            .WithName("Pam_AccessConnectors_Rotation_Jobs_GetAll")
            .WithDescription(
                "The calling access connector's currently claimable jobs on its assigned targets -- the " +
                "poll. Doubles as a heartbeat when idle. Heartbeat contract: an access connector MUST call " +
                "some connector-facing rotation endpoint at an interval shorter than ConnectorOfflineAfter " +
                "for as long as it holds a claim, or the release sweep may reclaim the job once the claim's " +
                "lease also expires; an access connector SHOULD poll no more often than " +
                "HeartbeatMinInterval, since the heartbeat write is conditional on that interval and " +
                "polling faster gains nothing.");

        group.MapPost("{id:guid}/claim", (Guid id, RotationJobEndpointsHandler handler) => handler.Claim(id))
            .WithName("Pam_AccessConnectors_Rotation_Jobs_Claim")
            .WithDescription(
                "Atomically claims a job -- first-claim-wins -- and returns the work snapshot needed to " +
                "execute the rotation. 409 means another access connector won the race (claim a different " +
                "job); 404 means this access connector was never eligible to claim it (no assignment, " +
                "wrong organization, or a disabled target/config).");

        return group;
    }
}
