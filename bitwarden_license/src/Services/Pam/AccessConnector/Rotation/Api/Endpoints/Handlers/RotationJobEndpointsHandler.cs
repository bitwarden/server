using Bit.Core.Context;
using Bit.HttpExtensions;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for the connector-facing <c>access-connectors/rotation/jobs</c> actions. The poll serves only an enabled
/// access connector, and only jobs on its organization's assigned target systems.
/// </summary>
public class RotationJobEndpointsHandler(
    ICurrentContext currentContext,
    IPamRotationJobRepository jobRepository,
    TimeProvider timeProvider,
    IClaimRotationJobCommand claimRotationJobCommand)
{
    public async Task<ListResponseModel<ClaimableRotationJobResponseModel>> GetJobs()
    {
        var connectorId = currentContext.PamAccessConnectorId!.Value;
        var jobs = await jobRepository.GetManyClaimableByAccessConnectorIdAsync(
            connectorId, timeProvider.GetUtcNow().UtcDateTime);

        return new ListResponseModel<ClaimableRotationJobResponseModel>(
            jobs.Select(job => new ClaimableRotationJobResponseModel(job)));
    }

    public async Task<RotationClaimResponseModel> Claim(Guid id)
    {
        var connectorId = currentContext.PamAccessConnectorId!.Value;
        var result = await claimRotationJobCommand.ClaimAsync(connectorId, id);
        return new RotationClaimResponseModel(result);
    }
}
