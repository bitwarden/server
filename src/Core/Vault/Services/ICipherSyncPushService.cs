using Bit.Core.Enums;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Vault.Services;

public interface ICipherSyncPushService
{
    Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds);
    Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds);
    Task PushSyncCipherDeleteAsync(Cipher cipher, IEnumerable<Guid>? collectionIds = null);

    /// <summary>
    /// Tells the user's other devices that the favorite, folder, or archive state of these ciphers changed.
    /// Sends one <see cref="PushType.SyncCipherUserFields"/> per cipher. When the feature flag is off,
    /// or there are too many ciphers, it sends one <see cref="PushType.SyncCiphers"/> instead.
    /// </summary>
    /// <param name="excludeCurrentContextFromSyncCiphers">
    /// Whether the <see cref="PushType.SyncCiphers"/> push skips the acting device.
    /// <see cref="PushType.SyncCipherUserFields"/> always skips it.
    /// </param>
    Task PushSyncCipherUserFieldsAsync(Guid userId, IReadOnlyCollection<CipherDetails> ciphers,
        bool excludeCurrentContextFromSyncCiphers);

    /// <inheritdoc cref="PushSyncCipherUserFieldsAsync(Guid, IReadOnlyCollection{CipherDetails}, bool)"/>
    /// <remarks>Loads the ciphers only when it sends per-cipher pushes.</remarks>
    Task PushSyncCipherUserFieldsAsync(Guid userId, IReadOnlyCollection<Guid> cipherIds,
        bool excludeCurrentContextFromSyncCiphers);
}
