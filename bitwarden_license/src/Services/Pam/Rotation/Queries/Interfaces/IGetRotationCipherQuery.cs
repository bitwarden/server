using Bit.Core.Vault.Entities;

namespace Bit.Services.Pam.Rotation.Queries.Interfaces;

public interface IGetRotationCipherQuery
{
    /// <summary>
    /// Returns the cipher for a daemon's claimed, executing attempt — deliberately narrow (only this daemon's
    /// in-flight attempt, never a general cipher read). Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> when the attempt does not exist, is not claimed by
    /// <paramref name="daemonId"/>, is not Executing, or its job is not Claimed by the same daemon.
    /// </summary>
    Task<Cipher> GetAsync(Guid daemonId, Guid attemptId);
}
