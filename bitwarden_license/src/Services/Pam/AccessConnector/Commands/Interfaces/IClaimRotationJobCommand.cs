using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IClaimRotationJobCommand
{
    /// <summary>
    /// Claims a job, first claim wins, and inserts its Executing attempt in the same transaction. Throws
    /// <see cref="Bit.Core.Exceptions.ConflictException"/> on a lost race and
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> if the access connector is not eligible.
    /// </summary>
    Task<PamRotationClaimResult> ClaimAsync(Guid accessConnectorId, Guid jobId);
}
