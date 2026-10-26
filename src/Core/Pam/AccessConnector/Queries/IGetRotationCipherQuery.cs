using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Pam.AccessConnector.Queries;

/// <summary>
/// The cipher for an executing rotation attempt, with the witness that authorizes emitting its full data.
/// </summary>
/// <param name="Cipher">The cipher the attempt targets, with its organization's <c>UseTotp</c>.</param>
/// <param name="Access">Minted by the leasing gate for <paramref name="Cipher"/> alone.</param>
public record RotationCipherAccess(CipherOrganizationDetails Cipher, FullCipherAccess Access);

public interface IGetRotationCipherQuery
{
    /// <summary>
    /// Returns the cipher for an access connector's claimed, executing attempt, never a general cipher read. Throws
    /// <see cref="Bit.Core.Exceptions.NotFoundException"/> unless the attempt and its job are both claimed by
    /// <paramref name="accessConnectorId"/> and still in progress.
    /// </summary>
    Task<RotationCipherAccess> GetAsync(Guid accessConnectorId, Guid attemptId);
}
