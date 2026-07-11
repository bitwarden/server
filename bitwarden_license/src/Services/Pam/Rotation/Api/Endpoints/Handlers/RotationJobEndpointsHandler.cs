using Bit.Core.Context;
using Bit.Services.Pam.Rotation.Api.Models.Response;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Api.Endpoints.Handlers;

/// <summary>
/// Handler for <c>POST rotation/jobs/{id}/claim</c> (spec <c>ClaimRotation</c>). Runs behind
/// <c>Policies.PamRotationDaemon</c>; <see cref="IClaimRotationJobCommand"/> throws 409 on a lost race and 404 when
/// the daemon was never eligible to claim the job (no assignment, wrong organization, disabled target/config).
/// </summary>
public class RotationJobEndpointsHandler(ICurrentContext currentContext, IClaimRotationJobCommand claimRotationJobCommand)
{
    public async Task<RotationClaimResponseModel> Claim(Guid id)
    {
        var daemonId = currentContext.PamDaemonId!.Value;
        var result = await claimRotationJobCommand.ClaimAsync(daemonId, id);
        return new RotationClaimResponseModel(result);
    }
}
