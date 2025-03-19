namespace Bit.Core.Vault.Commands.Interfaces;

public interface IArchiveCiphersCommand
{
    public Task ArchiveManyAsync(IEnumerable<Guid> cipherIds, Guid archivingUserId);
}
