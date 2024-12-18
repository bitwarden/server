#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Services;

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
    Task PushNotificationAsync(Notification notification);
    Task PushNotificationStatusAsync(Notification notification, NotificationStatus notificationStatus);
    Task PushAuthRequestAsync(AuthRequest authRequest);
    Task PushAuthRequestResponseAsync(AuthRequest authRequest);

    Task SendPayloadToInstallationAsync(string installationId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null);

    Task SendPayloadToUserAsync(string userId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null);

    Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null);
    Task PushSyncOrganizationStatusAsync(Organization organization);
}
