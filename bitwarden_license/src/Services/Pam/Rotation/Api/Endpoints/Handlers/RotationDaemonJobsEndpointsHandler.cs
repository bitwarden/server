using Bit.Core.Context;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for <c>GET rotation/daemon/jobs</c> -- the daemon's poll (spec <c>ClaimRotation</c>'s candidate set),
/// and the only request an idle daemon makes, which is why
/// <see cref="Bit.Services.Pam.Rotation.Api.Endpoints.Filters.DaemonHeartbeatEndpointFilter"/> records the heartbeat
/// for the whole daemon surface rather than this handler doing it. The daemon's identity comes from
/// <see cref="ICurrentContext.PamDaemonId"/>; the poll query admits only an Enabled daemon, and returns only jobs
/// belonging to that daemon's organization and assigned target systems.
/// </summary>
public class RotationDaemonJobsEndpointsHandler(
    ICurrentContext currentContext,
    IPamRotationJobRepository jobRepository,
    TimeProvider timeProvider)
{
    public async Task<ListResponseModel<ClaimableRotationJobResponseModel>> GetJobs()
    {
        var daemonId = currentContext.PamDaemonId!.Value;
        var jobs = await jobRepository.GetManyClaimableByDaemonIdAsync(
            daemonId, timeProvider.GetUtcNow().UtcDateTime);

        return new ListResponseModel<ClaimableRotationJobResponseModel>(
            jobs.Select(job => new ClaimableRotationJobResponseModel(job)));
    }
}
