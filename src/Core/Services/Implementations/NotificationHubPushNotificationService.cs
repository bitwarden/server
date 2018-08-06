using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Microsoft.Azure.NotificationHubs;
using Bit.Core.Enums;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Bit.Core.Models;

namespace Bit.Core.Services
{
    public class NotificationHubPushNotificationService : IPushNotificationService
    {
        private readonly GlobalSettings _globalSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private NotificationHubClient _client = null;
        private DateTime? _clientExpires = null;

        public NotificationHubPushNotificationService(
            GlobalSettings globalSettings,
            IHttpContextAccessor httpContextAccessor)
        {
            _globalSettings = globalSettings;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task PushSyncCipherCreateAsync(Cipher cipher)
        {
            await PushCipherAsync(cipher, PushType.SyncCipherCreate);
        }

        public async Task PushSyncCipherUpdateAsync(Cipher cipher)
        {
            await PushCipherAsync(cipher, PushType.SyncCipherUpdate);
        }

        public async Task PushSyncCipherDeleteAsync(Cipher cipher)
        {
            await PushCipherAsync(cipher, PushType.SyncLoginDelete);
        }

        private async Task PushCipherAsync(Cipher cipher, PushType type)
        {
            if(cipher.OrganizationId.HasValue)
            {
                // We cannot send org pushes since access logic is much more complicated than just the fact that they belong
                // to the organization. Potentially we could blindly send to just users that have the access all permission
                // device registration needs to be more granular to handle that appropriately. A more brute force approach could
                // me to send "full sync" push to all org users, but that has the potential to DDOS the API in bursts.

                // await SendPayloadToOrganizationAsync(cipher.OrganizationId.Value, type, message, true);
            }
            else if(cipher.UserId.HasValue)
            {
                var message = new SyncCipherPushNotification
                {
                    Id = cipher.Id,
                    UserId = cipher.UserId,
                    OrganizationId = cipher.OrganizationId,
                    RevisionDate = cipher.RevisionDate,
                };

                await SendPayloadToUserAsync(cipher.UserId.Value, type, message, true);
            }
        }

        public async Task PushSyncFolderCreateAsync(Folder folder)
        {
            await PushFolderAsync(folder, PushType.SyncFolderCreate);
        }

        public async Task PushSyncFolderUpdateAsync(Folder folder)
        {
            await PushFolderAsync(folder, PushType.SyncFolderUpdate);
        }

        public async Task PushSyncFolderDeleteAsync(Folder folder)
        {
            await PushFolderAsync(folder, PushType.SyncFolderDelete);
        }

        private async Task PushFolderAsync(Folder folder, PushType type)
        {
            var message = new SyncFolderPushNotification
            {
                Id = folder.Id,
                UserId = folder.UserId,
                RevisionDate = folder.RevisionDate
            };

            await SendPayloadToUserAsync(folder.UserId, type, message, true);
        }

        public async Task PushSyncCiphersAsync(Guid userId)
        {
            await PushSyncUserAsync(userId, PushType.SyncCiphers);
        }

        public async Task PushSyncVaultAsync(Guid userId)
        {
            await PushSyncUserAsync(userId, PushType.SyncVault);
        }

        public async Task PushSyncOrgKeysAsync(Guid userId)
        {
            await PushSyncUserAsync(userId, PushType.SyncOrgKeys);
        }

        public async Task PushSyncSettingsAsync(Guid userId)
        {
            await PushSyncUserAsync(userId, PushType.SyncSettings);
        }

        private async Task PushSyncUserAsync(Guid userId, PushType type)
        {
            var message = new SyncUserPushNotification
            {
                UserId = userId,
                Date = DateTime.UtcNow
            };

            await SendPayloadToUserAsync(userId, type, message, false);
        }

        private async Task SendPayloadToUserAsync(Guid userId, PushType type, object payload, bool excludeCurrentContext)
        {
            await SendPayloadToUserAsync(userId.ToString(), type, payload, GetContextIdentifier(excludeCurrentContext));
        }

        private async Task SendPayloadToOrganizationAsync(Guid orgId, PushType type, object payload, bool excludeCurrentContext)
        {
            await SendPayloadToUserAsync(orgId.ToString(), type, payload, GetContextIdentifier(excludeCurrentContext));
        }

        public async Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier)
        {
            var tag = BuildTag($"template:payload_userId:{userId}", identifier);
            await SendPayloadAsync(tag, type, payload);
        }

        public async Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier)
        {
            var tag = BuildTag($"template:payload && organizationId:{orgId}", identifier);
            await SendPayloadAsync(tag, type, payload);
        }

        private string GetContextIdentifier(bool excludeCurrentContext)
        {
            if(!excludeCurrentContext)
            {
                return null;
            }

            var currentContext = _httpContextAccessor?.HttpContext?.
                RequestServices.GetService(typeof(CurrentContext)) as CurrentContext;
            return currentContext?.DeviceIdentifier;
        }

        private string BuildTag(string tag, string identifier)
        {
            if(!string.IsNullOrWhiteSpace(identifier))
            {
                tag += $" && !deviceIdentifier:{identifier}";
            }

            return $"({tag})";
        }

        private async Task SendPayloadAsync(string tag, PushType type, object payload)
        {
            await RenewClientAndExecuteAsync(async client => await client.SendTemplateNotificationAsync(
                new Dictionary<string, string>
                {
                    { "type",  ((byte)type).ToString() },
                    { "payload", JsonConvert.SerializeObject(payload) }
                }, tag));
        }

        private async Task RenewClientAndExecuteAsync(Func<NotificationHubClient, Task> task)
        {
            var now = DateTime.UtcNow;
            if(_client == null || !_clientExpires.HasValue || _clientExpires.Value < now)
            {
                _clientExpires = now.Add(TimeSpan.FromMinutes(30));
                _client = NotificationHubClient.CreateClientFromConnectionString(
                    _globalSettings.NotificationHub.ConnectionString,
                    _globalSettings.NotificationHub.HubName);
            }
            await task(_client);
        }
    }
}
