namespace Bit.Core.Vault.Commands.Interfaces;

public interface IUnarchiveCiphersCommand
{
    public Task UnarchiveManyAsync(IEnumerable<Guid> cipherIds, Guid unarchivingUserId);
}
