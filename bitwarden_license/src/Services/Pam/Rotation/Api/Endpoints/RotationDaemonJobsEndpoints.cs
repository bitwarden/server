using Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

namespace Bit.Services.Pam.Rotation.Api.Endpoints;

/// <summary>The daemon-facing <c>rotation/daemon</c> resource: the poll.</summary>
internal static class RotationDaemonJobsEndpoints
{
    public static RouteGroupBuilder MapRotationDaemonJobsEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("PamRotationDaemonJobs");

        group.MapGet("jobs", (RotationDaemonJobsEndpointsHandler handler) => handler.GetJobs())
            .WithName("Pam_Rotation_DaemonJobs_GetAll")
            .WithDescription(
                "A daemon's currently claimable jobs on its assigned targets -- the poll. Doubles as a heartbeat " +
                "when idle. Heartbeat contract: a daemon MUST call some daemon-facing rotation endpoint at an " +
                "interval shorter than DaemonOfflineAfter for as long as it holds a claim, or the release sweep " +
                "may reclaim the job once the claim's lease also expires; a daemon SHOULD poll no more often than " +
                "HeartbeatMinInterval, since the heartbeat write is conditional on that interval and polling faster " +
                "gains nothing.");

        return group;
    }
}
