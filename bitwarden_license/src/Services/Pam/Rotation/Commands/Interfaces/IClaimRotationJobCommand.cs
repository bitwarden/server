using Bit.Pam.Models;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IClaimRotationJobCommand
{
    /// <summary>
    /// Claims a rotation job for a daemon (spec <c>ClaimRotation</c>) — atomic first-claim-wins, inserting the
    /// Executing attempt in the same transaction. Callers are expected to already be authenticated as the daemon
    /// (bearer token per request is the eligibility re-validation). Throws
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> when the job was not claimable (lost race — 409, retry a
    /// different job) and <see cref="Bit.Core.Exceptions.NotFoundException"/> when the daemon was never eligible to
    /// claim it (no assignment, wrong organization, disabled target/config — 404, never leaked as a distinct
    /// error).
    /// </summary>
    Task<PamRotationClaimResult> ClaimAsync(Guid daemonId, Guid jobId);
}
