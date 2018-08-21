using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services
{
    public class MultiServicePushNotificationService : IPushNotificationService
    {
        private readonly List<IPushNotificationService> _services = new List<IPushNotificationService>();

        public MultiServicePushNotificationService(
            GlobalSettings globalSettings,
            IHttpContextAccessor httpContextAccessor,
            ILogger<RelayPushNotificationService> relayLogger,
            ILogger<NotificationsApiPushNotificationService> hubLogger)
        {
            if(globalSettings.SelfHosted)
            {
                if(CoreHelpers.SettingHasValue(globalSettings.PushRelayBaseUri) &&
                    globalSettings.Installation?.Id != null &&
                    CoreHelpers.SettingHasValue(globalSettings.Installation?.Key))
                {
                    _services.Add(new RelayPushNotificationService(globalSettings, httpContextAccessor, relayLogger));
                }
                if(CoreHelpers.SettingHasValue(globalSettings.InternalIdentityKey) &&
                    CoreHelpers.SettingHasValue(globalSettings.BaseServiceUri.InternalNotifications))
                {
                    _services.Add(new NotificationsApiPushNotificationService(
                        globalSettings, httpContextAccessor, hubLogger));
                }
            }
            else
            {
                if(CoreHelpers.SettingHasValue(globalSettings.NotificationHub.ConnectionString))
                {
                    _services.Add(new NotificationHubPushNotificationService(globalSettings, httpContextAccessor));
                }
                if(CoreHelpers.SettingHasValue(globalSettings.Notifications?.ConnectionString))
                {
                    _services.Add(new AzureQueuePushNotificationService(globalSettings, httpContextAccessor));
                }
            }
        }

        public Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            PushToServices((s) => s.PushSyncCipherCreateAsync(cipher, collectionIds));
            return Task.FromResult(0);
        }

        public Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            PushToServices((s) => s.PushSyncCipherUpdateAsync(cipher, collectionIds));
            return Task.FromResult(0);
        }

        public Task PushSyncCipherDeleteAsync(Cipher cipher)
        {
            PushToServices((s) => s.PushSyncCipherDeleteAsync(cipher));
            return Task.FromResult(0);
        }

        public Task PushSyncFolderCreateAsync(Folder folder)
        {
            PushToServices((s) => s.PushSyncFolderCreateAsync(folder));
            return Task.FromResult(0);
        }

        public Task PushSyncFolderUpdateAsync(Folder folder)
        {
            PushToServices((s) => s.PushSyncFolderUpdateAsync(folder));
            return Task.FromResult(0);
        }

        public Task PushSyncFolderDeleteAsync(Folder folder)
        {
            PushToServices((s) => s.PushSyncFolderDeleteAsync(folder));
            return Task.FromResult(0);
        }

        public Task PushSyncCiphersAsync(Guid userId)
        {
            PushToServices((s) => s.PushSyncCiphersAsync(userId));
            return Task.FromResult(0);
        }

        public Task PushSyncVaultAsync(Guid userId)
        {
            PushToServices((s) => s.PushSyncVaultAsync(userId));
            return Task.FromResult(0);
        }

        public Task PushSyncOrgKeysAsync(Guid userId)
        {
            PushToServices((s) => s.PushSyncOrgKeysAsync(userId));
            return Task.FromResult(0);
        }

        public Task PushSyncSettingsAsync(Guid userId)
        {
            PushToServices((s) => s.PushSyncSettingsAsync(userId));
            return Task.FromResult(0);
        }

        public Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier)
        {
            PushToServices((s) => s.SendPayloadToUserAsync(userId, type, payload, identifier));
            return Task.FromResult(0);
        }

        public Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier)
        {
            PushToServices((s) => s.SendPayloadToOrganizationAsync(orgId, type, payload, identifier));
            return Task.FromResult(0);
        }

        private void PushToServices(Func<IPushNotificationService, Task> pushFunc)
        {
            if(_services != null)
            {
                foreach(var service in _services)
                {
                    pushFunc(service);
                }
            }
        }
    }
}
