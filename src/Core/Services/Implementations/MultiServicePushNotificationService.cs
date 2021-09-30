using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Core.Services
{
    public class MultiServicePushNotificationService : IPushNotificationService
    {
        private readonly List<IPushNotificationService> _services = new List<IPushNotificationService>();
        private readonly ILogger<MultiServicePushNotificationService> _logger;

        public MultiServicePushNotificationService(
            IDeviceRepository deviceRepository,
            IInstallationDeviceRepository installationDeviceRepository,
            GlobalSettings globalSettings,
            IHttpContextAccessor httpContextAccessor,
            ILogger<MultiServicePushNotificationService> logger,
            ILogger<RelayPushNotificationService> relayLogger,
            ILogger<NotificationsApiPushNotificationService> hubLogger)
        {
            if (globalSettings.SelfHosted)
            {
                if (CoreHelpers.SettingHasValue(globalSettings.PushRelayBaseUri) &&
                    globalSettings.Installation?.Id != null &&
                    CoreHelpers.SettingHasValue(globalSettings.Installation?.Key))
                {
                    _services.Add(new RelayPushNotificationService(deviceRepository, globalSettings,
                        httpContextAccessor, relayLogger));
                }
                if (CoreHelpers.SettingHasValue(globalSettings.InternalIdentityKey) &&
                    CoreHelpers.SettingHasValue(globalSettings.BaseServiceUri.InternalNotifications))
                {
                    _services.Add(new NotificationsApiPushNotificationService(
                        globalSettings, httpContextAccessor, hubLogger));
                }
            }
            else
            {
                if (CoreHelpers.SettingHasValue(globalSettings.NotificationHub.ConnectionString))
                {
                    _services.Add(new NotificationHubPushNotificationService(installationDeviceRepository,
                        globalSettings, httpContextAccessor));
                }
                if (CoreHelpers.SettingHasValue(globalSettings.Notifications?.ConnectionString))
                {
                    _services.Add(new AzureQueuePushNotificationService(globalSettings, httpContextAccessor));
                }
            }
            
            _logger = logger;
        }

        public async Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            await PushToServices(async (s) => await s.PushSyncCipherCreateAsync(cipher, collectionIds));
        }

        public async Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            await PushToServices(async (s) => await s.PushSyncCipherUpdateAsync(cipher, collectionIds));
        }

        public async Task PushSyncCipherDeleteAsync(Cipher cipher)
        {
            await PushToServices(async (s) => await s.PushSyncCipherDeleteAsync(cipher));
        }

        public async Task PushSyncFolderCreateAsync(Folder folder)
        {
            await PushToServices(async (s) => await s.PushSyncFolderCreateAsync(folder));
        }

        public async Task PushSyncFolderUpdateAsync(Folder folder)
        {
            await PushToServices(async (s) => await s.PushSyncFolderUpdateAsync(folder));
        }

        public async Task PushSyncFolderDeleteAsync(Folder folder)
        {
            await PushToServices(async (s) => await s.PushSyncFolderDeleteAsync(folder));
        }

        public async Task PushSyncCiphersAsync(Guid userId)
        {
            await PushToServices(async (s) => await s.PushSyncCiphersAsync(userId));
        }

        public async Task PushSyncVaultAsync(Guid userId)
        {
            await PushToServices(async (s) => await s.PushSyncVaultAsync(userId));
        }

        public async Task PushSyncOrgKeysAsync(Guid userId)
        {
            await PushToServices(async (s) => await s.PushSyncOrgKeysAsync(userId));
        }

        public async Task PushSyncSettingsAsync(Guid userId)
        {
            await PushToServices(async (s) => await s.PushSyncSettingsAsync(userId));
        }

        public async Task PushLogOutAsync(Guid userId)
        {
            await PushToServices(async (s) => await s.PushLogOutAsync(userId));
        }

        public async Task PushSyncSendCreateAsync(Send send)
        {
            await PushToServices(async (s) => await s.PushSyncSendCreateAsync(send));
        }

        public async Task PushSyncSendUpdateAsync(Send send)
        {
            await PushToServices(async (s) => await s.PushSyncSendUpdateAsync(send));
        }

        public async Task PushSyncSendDeleteAsync(Send send)
        {
            await PushToServices(async (s) => await s.PushSyncSendDeleteAsync(send));
        }

        public async Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier,
            string deviceId = null)
        {
            await PushToServices(async (s) => await s.SendPayloadToUserAsync(userId, type, payload, identifier, deviceId));
        }

        public async Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier,
            string deviceId = null)
        {
            await PushToServices(async (s) => await s.SendPayloadToOrganizationAsync(orgId, type, payload, identifier, deviceId));
        }

        private async Task PushToServices(Func<IPushNotificationService, Task> pushFunc)
        {
            if (_services != null)
            {
                foreach (var service in _services)
                {
                    try
                    {
                        await pushFunc(service);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, ex.Message);
                    }
                }
            }
        }
    }
}
