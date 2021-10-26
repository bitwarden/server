using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Context;
using Bit.Core.Enums;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Bit.Core.Models;
using Bit.Core.Settings;
using Amazon.SimpleNotificationService;
using Amazon;
using Amazon.SimpleNotificationService.Model;

namespace Bit.Core.Services
{
    public class AmazonSnsPushNotificationService : IPushNotificationService
    {
        private readonly GlobalSettings _globalSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private AmazonSimpleNotificationServiceClient _client = null;

        public AmazonSnsPushNotificationService(
            GlobalSettings globalSettings,
            IHttpContextAccessor httpContextAccessor)
        {
            _globalSettings = globalSettings;
            _httpContextAccessor = httpContextAccessor;
            _client = new AmazonSimpleNotificationServiceClient(
                globalSettings.Amazon.AccessKeyId,
                globalSettings.Amazon.AccessKeySecret,
                RegionEndpoint.GetBySystemName(globalSettings.Amazon.Region));
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
            if (cipher.OrganizationId.HasValue)
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

        public async Task PushSyncSendCreateAsync(Send send)
        {
            await PushSendAsync(send, PushType.SyncSendCreate);
        }

        public async Task PushSyncSendUpdateAsync(Send send)
        {
            await PushSendAsync(send, PushType.SyncSendUpdate);
        }

        public async Task PushSyncSendDeleteAsync(Send send)
        {
            await PushSendAsync(send, PushType.SyncSendDelete);
        }

        private async Task PushSendAsync(Send send, PushType type)
        {
            if(send.UserId.HasValue)
            {
                var message = new SyncSendPushNotification
                {
                    Id = send.Id,
                    UserId = send.UserId.Value,
                    RevisionDate = send.RevisionDate
                };

                await SendPayloadToUserAsync(message.UserId, type, message, true);
            }
        }

        private async Task SendPayloadToUserAsync(Guid userId, PushType type, object payload,
            bool excludeCurrentContext)
        {
            await SendPayloadToUserAsync(userId.ToString(), type, payload, GetContextIdentifier(excludeCurrentContext));
        }

        private async Task SendPayloadToOrganizationAsync(Guid orgId, PushType type, object payload,
            bool excludeCurrentContext)
        {
            await SendPayloadToUserAsync(orgId.ToString(), type, payload, GetContextIdentifier(excludeCurrentContext));
        }

        public async Task SendPayloadToUserAsync(string userId, PushType type, object payload,
            string excludedIdentifier, string deviceId = null)
        {
            await SendPayloadAsync(string.Concat("push__userId__", userId), type, payload);
        }

        public async Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload,
            string excludedIdentifier,  string deviceId = null)
        {
            await SendPayloadAsync(string.Concat("push__organizationId__", orgId), type, payload);
        }

        private string GetContextIdentifier(bool excludeCurrentContext)
        {
            if(!excludeCurrentContext)
            {
                return null;
            }

            var currentContext = _httpContextAccessor?.HttpContext?.
                RequestServices.GetService(typeof(ICurrentContext)) as ICurrentContext;
            return currentContext?.DeviceIdentifier;
        }

        private async Task SendPayloadAsync(string topicName, PushType type, object payload)
        {
            await _client.PublishAsync(new PublishRequest
            {
                TopicArn = CreateTopicArn(topicName),
                Message = JsonConvert.SerializeObject(new
                {
                    data = new
                    {
                        type = type,
                        payload = payload
                    }
                })
            });
        }

        private string CreateTopicArn(string topicName)
        {
            return string.Format("arn:aws:sns:{0}:{1}:{2}",
                _globalSettings.Amazon.Region, _globalSettings.Amazon.AccountId, topicName);
        }
    }
}
