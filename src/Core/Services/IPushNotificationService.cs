using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public interface IPushNotificationService
    {
        Task PushSyncCipherCreateAsync(Cipher cipher);
        Task PushSyncCipherUpdateAsync(Cipher cipher);
        Task PushSyncCipherDeleteAsync(Cipher cipher);
        Task PushSyncFolderCreateAsync(Folder folder);
        Task PushSyncFolderUpdateAsync(Folder folder);
        Task PushSyncFolderDeleteAsync(Folder folder);
        Task PushSyncCiphersAsync(Guid userId);
        Task PushSyncVaultAsync(Guid userId);
        Task PushSyncOrgKeysAsync(Guid userId);
        Task PushSyncSettingsAsync(Guid userId);
        Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier);
        Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier);
    }
}
