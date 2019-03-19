using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Microsoft.AspNetCore.Http;
using Bit.Core.Models;
using System.Net.Http;
using Bit.Core.Models.Api;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class RelayPushNotificationService : BaseIdentityClientService, IPushNotificationService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<RelayPushNotificationService> _logger;

        public RelayPushNotificationService(
            IDeviceRepository deviceRepository,
            GlobalSettings globalSettings,
            IHttpContextAccessor httpContextAccessor,
            ILogger<RelayPushNotificationService> logger)
            : base(
                  globalSettings.PushRelayBaseUri,
                  globalSettings.Installation.IdentityUri,
                  "api.push",
                  $"installation.{globalSettings.Installation.Id}",
                  globalSettings.Installation.Key,
                  logger)
        {
            _deviceRepository = deviceRepository;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            await PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);
        }

        public async Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            await PushCipherAsync(cipher, PushType.SyncCipherUpdate, collectionIds);
        }

        public async Task PushSyncCipherDeleteAsync(Cipher cipher)
        {
            await PushCipherAsync(cipher, PushType.SyncLoginDelete, null);
        }

        private async Task PushCipherAsync(Cipher cipher, PushType type, IEnumerable<Guid> collectionIds)
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
            await PushUserAsync(userId, PushType.SyncCiphers);
        }

        public async Task PushSyncVaultAsync(Guid userId)
        {
            await PushUserAsync(userId, PushType.SyncVault);
        }

        public async Task PushSyncOrgKeysAsync(Guid userId)
        {
            await PushUserAsync(userId, PushType.SyncOrgKeys);
        }

        public async Task PushSyncSettingsAsync(Guid userId)
        {
            await PushUserAsync(userId, PushType.SyncSettings);
        }

        public async Task PushLogOutAsync(Guid userId)
        {
            await PushUserAsync(userId, PushType.LogOut);
        }

        private async Task PushUserAsync(Guid userId, PushType type)
        {
            var message = new UserPushNotification
            {
                UserId = userId,
                Date = DateTime.UtcNow
            };

            await SendPayloadToUserAsync(userId, type, message, false);
        }

        private async Task SendPayloadToUserAsync(Guid userId, PushType type, object payload, bool excludeCurrentContext)
        {
            var request = new PushSendRequestModel
            {
                UserId = userId.ToString(),
                Type = type,
                Payload = payload
            };
            
            await AddCurrentContextAsync(request, excludeCurrentContext);
            await SendAsync(HttpMethod.Post, "push/send", request);
        }

        private async Task SendPayloadToOrganizationAsync(Guid orgId, PushType type, object payload, bool excludeCurrentContext)
        {
            var request = new PushSendRequestModel
            {
                OrganizationId = orgId.ToString(),
                Type = type,
                Payload = payload
            };

            await AddCurrentContextAsync(request, excludeCurrentContext);
            await SendAsync(HttpMethod.Post, "push/send", request);
        }

        private async Task AddCurrentContextAsync(PushSendRequestModel request, bool addIdentifier)
        {
            var currentContext = _httpContextAccessor?.HttpContext?.
                RequestServices.GetService(typeof(CurrentContext)) as CurrentContext;
            if(!string.IsNullOrWhiteSpace(currentContext?.DeviceIdentifier))
            {
                var device = await _deviceRepository.GetByIdentifierAsync(currentContext.DeviceIdentifier);
                if(device != null)
                {
                    request.DeviceId = device.Id.ToString();
                }
                if(addIdentifier)
                {
                    request.Identifier = currentContext.DeviceIdentifier;
                }
            }
        }

        public Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier,
            string deviceId = null)
        {
            throw new NotImplementedException();
        }

        public Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier,
            string deviceId = null)
        {
            throw new NotImplementedException();
        }
    }
}
