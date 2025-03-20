using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Commands.Interfaces;

public interface IUnarchiveCiphersCommand
{
    public Task<ICollection<CipherOrganizationDetails>> UnarchiveManyAsync(IEnumerable<Guid> cipherIds, Guid unarchivingUserId);
}
