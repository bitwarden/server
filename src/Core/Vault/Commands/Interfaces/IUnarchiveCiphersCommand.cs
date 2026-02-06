using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface IUnarchiveCiphersCommand
{
    /// <summary>
    /// Unarchives a cipher. This nulls the ArchivedDate property on a Cipher.
    /// </summary>
    /// <param name="cipherIds">Cipher ID to unarchive.</param>
    /// <param name="unarchivingUserId">User ID to check against the Ciphers that are trying to be unarchived.</param>
    /// <returns></returns>
    public Task<ICollection<CipherDetails>> UnarchiveManyAsync(IEnumerable<Guid> cipherIds, Guid unarchivingUserId);
}
