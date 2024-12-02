using Bit.Core.Auth.Entities;
using Bit.Core.Enums;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Platform.Push;

public interface IPushNotificationService
{
    Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds);
    Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds);
    Task PushSyncCipherDeleteAsync(Cipher cipher);
    Task PushSyncFolderCreateAsync(Folder folder);
    Task PushSyncFolderUpdateAsync(Folder folder);
    Task PushSyncFolderDeleteAsync(Folder folder);
    Task PushSyncCiphersAsync(Guid userId);
    Task PushSyncVaultAsync(Guid userId);
    Task PushSyncOrganizationsAsync(Guid userId);
    Task PushSyncOrgKeysAsync(Guid userId);
    Task PushSyncSettingsAsync(Guid userId);
    Task PushLogOutAsync(Guid userId, bool excludeCurrentContextFromPush = false);
    Task PushSyncSendCreateAsync(Send send);
    Task PushSyncSendUpdateAsync(Send send);
    Task PushSyncSendDeleteAsync(Send send);
    Task PushAuthRequestAsync(AuthRequest authRequest);
    Task PushAuthRequestResponseAsync(AuthRequest authRequest);
    Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier, string deviceId = null);
    Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier,
        string deviceId = null);
}
