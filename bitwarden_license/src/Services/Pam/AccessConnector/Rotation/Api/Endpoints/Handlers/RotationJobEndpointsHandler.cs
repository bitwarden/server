using Bit.Core.Context;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the connector-facing <c>access-connectors/rotation/jobs</c> actions. The poll is the only request an
/// idle access connector makes, which is why
/// <see cref="Bit.Services.Pam.AccessConnector.Api.Endpoints.Filters.AccessConnectorHeartbeatEndpointFilter"/>
/// records the heartbeat for the whole connector surface rather than this handler doing it. The connector's identity
/// comes from <see cref="ICurrentContext.PamDaemonId"/>; the poll query admits only an Enabled connector, and returns
/// only jobs belonging to that connector's organization and assigned target systems.
///
/// <see cref="IClaimRotationJobCommand"/> throws 409 on a lost race and 404 when the connector was never eligible to
/// claim the job (no assignment, wrong organization, disabled target/config).
/// </summary>
public class RotationJobEndpointsHandler(
    ICurrentContext currentContext,
    IPamRotationJobRepository jobRepository,
    TimeProvider timeProvider,
    IClaimRotationJobCommand claimRotationJobCommand)
{
    public async Task<ListResponseModel<ClaimableRotationJobResponseModel>> GetJobs()
    {
        var connectorId = currentContext.PamDaemonId!.Value;
        var jobs = await jobRepository.GetManyClaimableByDaemonIdAsync(
            connectorId, timeProvider.GetUtcNow().UtcDateTime);

        return new ListResponseModel<ClaimableRotationJobResponseModel>(
            jobs.Select(job => new ClaimableRotationJobResponseModel(job)));
    }

    public async Task<RotationClaimResponseModel> Claim(Guid id)
    {
        var connectorId = currentContext.PamDaemonId!.Value;
        var result = await claimRotationJobCommand.ClaimAsync(connectorId, id);
        return new RotationClaimResponseModel(result);
    }
}
