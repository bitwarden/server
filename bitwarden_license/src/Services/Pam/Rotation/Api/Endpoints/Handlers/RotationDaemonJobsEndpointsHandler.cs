using Bit.Core.Context;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for <c>GET rotation/daemon/jobs</c> -- the daemon's poll (spec <c>ClaimRotation</c>'s candidate set),
/// which doubles as its heartbeat when idle (the heartbeat write itself happens in
/// <see cref="Bit.Services.Pam.Rotation.Api.Endpoints.Filters.DaemonRequestEndpointFilter"/>, ahead of every daemon
/// route, not here). Runs behind <c>Policies.PamRotationDaemon</c>; the daemon's identity comes from
/// <see cref="ICurrentContext.PamDaemonId"/>, already re-verified Enabled by the filter.
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
