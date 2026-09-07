using Bit.Core.Vault.Entities;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IGetRotationCipherQuery
{
    /// <summary>
    /// Returns the cipher for a daemon's claimed, executing attempt — deliberately narrow, never a general
    /// cipher read. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> unless the attempt exists, is
    /// claimed by <paramref name="daemonId"/>, is Executing, and its job is Claimed by the same daemon.
    /// </summary>
    Task<Cipher> GetAsync(Guid daemonId, Guid attemptId);
}
