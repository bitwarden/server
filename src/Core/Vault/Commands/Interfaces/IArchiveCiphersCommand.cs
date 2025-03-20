using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface IArchiveCiphersCommand
{
    public Task<ICollection<CipherOrganizationDetails>> ArchiveManyAsync(IEnumerable<Guid> cipherIds, Guid archivingUserId);
}
