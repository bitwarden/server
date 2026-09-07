using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IClaimRotationJobCommand
{
    /// <summary>
    /// Claims a rotation job for a daemon — atomic first-claim-wins, inserting the Executing attempt in the
    /// same transaction. Throws <see cref="Bit.Core.Exceptions.ConflictException"/> if the job was not
    /// claimable (lost race), or <see cref="Bit.Core.Exceptions.NotFoundException"/> if the daemon was never
    /// eligible to claim it.
    /// </summary>
    Task<PamRotationClaimResult> ClaimAsync(Guid daemonId, Guid jobId);
}
