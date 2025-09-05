using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface IArchiveCiphersCommand
{
    /// <summary>
    /// Archives a cipher. This fills in the ArchivedDate property on a Cipher.
    /// </summary>
    /// <param name="cipherIds">Cipher ID to archive.</param>
    /// <param name="archivingUserId">User ID to check against the Ciphers that are trying to be archived.</param>
    /// <returns></returns>
    public Task<ICollection<CipherOrganizationDetails>> ArchiveManyAsync(IEnumerable<Guid> cipherIds, Guid archivingUserId);
}
