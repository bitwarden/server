using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopPushNotificationService : IPushNotificationService
    {
        public Task PushSyncCipherCreateAsync(Cipher cipher)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncCipherDeleteAsync(Cipher cipher)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncCiphersAsync(Guid userId)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncCipherUpdateAsync(Cipher cipher)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncFolderCreateAsync(Folder folder)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncFolderDeleteAsync(Folder folder)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncFolderUpdateAsync(Folder folder)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncOrgKeysAsync(Guid userId)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncSettingsAsync(Guid userId)
        {
            return Task.FromResult(0);
        }

        public Task PushSyncVaultAsync(Guid userId)
        {
            return Task.FromResult(0);
        }
    }
}
