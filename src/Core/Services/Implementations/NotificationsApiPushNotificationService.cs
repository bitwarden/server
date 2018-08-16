using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Newtonsoft.Json;
using Bit.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace Bit.Core.Services
{
    public class NotificationsApiPushNotificationService : BaseIdentityClientService, IPushNotificationService
    {
        private readonly GlobalSettings _globalSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public NotificationsApiPushNotificationService(
            GlobalSettings globalSettings,
            IHttpContextAccessor httpContextAccessor,
            ILogger<NotificationsApiPushNotificationService> logger)
            : base(
                 globalSettings.BaseServiceUri.InternalNotifications,
                 globalSettings.BaseServiceUri.InternalIdentity,
                 "internal",
                 $"internal.{globalSettings.ProjectName}",
                 globalSettings.InternalIdentityKey,
                 logger)
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
                var message = new SyncCipherPushNotification
                {
                    Id = cipher.Id,
                    OrganizationId = cipher.OrganizationId,
                    RevisionDate = cipher.RevisionDate,
                };

                await SendMessageAsync(type, message, true);
            }
            else if(cipher.UserId.HasValue)
            {
                var message = new SyncCipherPushNotification
                {
                    Id = cipher.Id,
                    UserId = cipher.UserId,
                    RevisionDate = cipher.RevisionDate,
                };

                await SendMessageAsync(type, message, true);
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

            await SendMessageAsync(type, message, true);
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

            await SendMessageAsync(type, message, false);
        }

        private async Task SendMessageAsync<T>(PushType type, T payload, bool excludeCurrentContext)
        {
            var contextId = GetContextIdentifier(excludeCurrentContext);
            var request = new PushNotificationData<T>(type, payload, contextId);
            await SendAsync(HttpMethod.Post, "/notification", request);
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

        public Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier)
        {
            // Noop
            return Task.FromResult(0);
        }

        public Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier)
        {
            // Noop
            return Task.FromResult(0);
        }
    }
}
