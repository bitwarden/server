using Bit.Core.Vault.Entities;

namespace Bit.Core.Vault.Services;

public interface ICipherSyncPushService
{
    Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds);
    Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds);
    Task PushSyncCipherDeleteAsync(Cipher cipher, IEnumerable<Guid>? collectionIds = null);
}
