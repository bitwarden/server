using Bit.Core.Vault.Entities;

namespace Bit.Services.Pam.AccessConnector.Queries.Interfaces;

public interface IGetRotationCipherQuery
{
    /// <summary>
    /// Returns the cipher for an access connector's claimed, executing attempt, never a general cipher read. Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> unless the attempt and its job are both claimed by
    /// <paramref name="accessConnectorId"/> and still in progress.
    /// </summary>
    Task<Cipher> GetAsync(Guid accessConnectorId, Guid attemptId);
}
