using Bit.Core.Context;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Api.Models.Response;
using Bit.Services.Pam.Rotation.Queries.Interfaces;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for <c>GET rotation/daemon/jobs</c> -- the daemon's poll (spec <c>ClaimRotation</c>'s candidate set),
/// which doubles as its heartbeat when idle (the heartbeat write itself happens in
/// <see cref="Bit.Services.Pam.Rotation.Api.Endpoints.Filters.DaemonRequestEndpointFilter"/>, ahead of every daemon
/// route, not here). Runs behind <c>Policies.PamRotationDaemon</c>; the daemon's identity comes from
/// <see cref="ICurrentContext.PamDaemonId"/>, already re-verified Enrolled by the filter.
/// </summary>
public class RotationDaemonJobsEndpointsHandler(
    ICurrentContext currentContext,
    IListClaimableJobsQuery listClaimableJobsQuery,
    IPamRotationConfigRepository configRepository)
{
    public async Task<ListResponseModel<ClaimableRotationJobResponseModel>> GetJobs()
    {
        var daemonId = currentContext.PamDaemonId!.Value;
        var jobs = await listClaimableJobsQuery.ListAsync(daemonId);

        // A claimable job carries only its RotationConfigId; the wire shape wants the target system directly, so
        // resolve it per job through the config. Jobs and their config are deleted together (DeleteRotationConfig
        // cascades), so a missing config here would mean the config disappeared mid-poll -- skip it rather than 500.
        var models = new List<ClaimableRotationJobResponseModel>(jobs.Count);
        foreach (var job in jobs)
        {
            var config = await configRepository.GetByIdAsync(job.RotationConfigId);
            if (config is not null)
            {
                models.Add(new ClaimableRotationJobResponseModel(job, config.TargetSystemId));
            }
        }

        return new ListResponseModel<ClaimableRotationJobResponseModel>(models);
    }
}
