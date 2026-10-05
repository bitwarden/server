using Bit.Core.Vault.Entities;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IGetRotationCipherQuery
{
    /// <summary>
    /// Returns the cipher for an access connector's claimed, executing attempt — deliberately narrow, never a general
    /// cipher read. Throws <see cref="Bit.Core.Exceptions.NotFoundException"/> unless the attempt exists, is
    /// claimed by <paramref name="accessConnectorId"/>, is Executing, and its job is Claimed by the same daemon.
    /// </summary>
    Task<Cipher> GetAsync(Guid accessConnectorId, Guid attemptId);
}
