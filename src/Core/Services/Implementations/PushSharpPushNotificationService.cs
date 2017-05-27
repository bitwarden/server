using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Newtonsoft.Json.Linq;
using PushSharp.Google;
using PushSharp.Apple;
using Microsoft.AspNetCore.Hosting;
using PushSharp.Core;
using Bit.Core.Models.Table;
using Bit.Core.Enums;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Services
{
    [Obsolete]
    public class PushSharpPushNotificationService : IPushNotificationService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILogger<IPushNotificationService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private GcmServiceBroker _gcmBroker;
        private ApnsServiceBroker _apnsBroker;

        public PushSharpPushNotificationService(
            IDeviceRepository deviceRepository,
            IHttpContextAccessor httpContextAccessor,
            ILogger<IPushNotificationService> logger,
            IHostingEnvironment hostingEnvironment,
            GlobalSettings globalSettings)
        {
            _deviceRepository = deviceRepository;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;

            InitGcmBroker(globalSettings);
            InitApnsBroker(globalSettings, hostingEnvironment);
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
            switch(cipher.Type)
            {
                case CipherType.Login:
                    await PushCipherAsync(cipher, PushType.SyncLoginDelete);
                    break;
                default:
                    break;
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

        private async Task PushCipherAsync(Cipher cipher, PushType type)
        {
            if(!cipher.UserId.HasValue)
            {
                // No push for org ciphers at the moment.
                return;
            }

            var message = new SyncCipherPushNotification
            {
                Type = type,
                Id = cipher.Id,
                UserId = cipher.UserId,
                OrganizationId = cipher.OrganizationId,
                RevisionDate = cipher.RevisionDate,
                Aps = new PushNotification.AppleData { ContentAvailable = 1 }
            };

            var excludedTokens = new List<string>();
            var currentContext = _httpContextAccessor?.HttpContext?.
                RequestServices.GetService(typeof(CurrentContext)) as CurrentContext;
            if(!string.IsNullOrWhiteSpace(currentContext?.DeviceIdentifier))
            {
                excludedTokens.Add(currentContext.DeviceIdentifier);
            }

            await PushToAllUserDevicesAsync(cipher.UserId.Value, JObject.FromObject(message), excludedTokens);
        }

        private async Task PushFolderAsync(Folder folder, PushType type)
        {
            var message = new SyncFolderPushNotification
            {
                Type = type,
                Id = folder.Id,
                UserId = folder.UserId,
                RevisionDate = folder.RevisionDate,
                Aps = new PushNotification.AppleData { ContentAvailable = 1 }
            };

            var excludedTokens = new List<string>();
            var currentContext = _httpContextAccessor?.HttpContext?.
                RequestServices.GetService(typeof(CurrentContext)) as CurrentContext;
            if(!string.IsNullOrWhiteSpace(currentContext?.DeviceIdentifier))
            {
                excludedTokens.Add(currentContext.DeviceIdentifier);
            }

            await PushToAllUserDevicesAsync(folder.UserId, JObject.FromObject(message), excludedTokens);
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
                Type = type,
                UserId = userId,
                Date = DateTime.UtcNow,
                Aps = new PushNotification.AppleData { ContentAvailable = 1 }
            };

            await PushToAllUserDevicesAsync(userId, JObject.FromObject(message), null);
        }

        private void InitGcmBroker(GlobalSettings globalSettings)
        {
            if(string.IsNullOrWhiteSpace(globalSettings.Push.GcmSenderId) || string.IsNullOrWhiteSpace(globalSettings.Push.GcmApiKey)
                || string.IsNullOrWhiteSpace(globalSettings.Push.GcmAppPackageName))
            {
                return;
            }

            var gcmConfig = new GcmConfiguration(globalSettings.Push.GcmSenderId, globalSettings.Push.GcmApiKey,
                globalSettings.Push.GcmAppPackageName);

            _gcmBroker = new GcmServiceBroker(gcmConfig);
            _gcmBroker.OnNotificationFailed += GcmBroker_OnNotificationFailed;
            _gcmBroker.OnNotificationSucceeded += (notification) =>
            {
                Debug.WriteLine("GCM Notification Sent!");
            };
            _gcmBroker.Start();
        }

        private void GcmBroker_OnNotificationFailed(GcmNotification notification, AggregateException exception)
        {
            exception.Handle(ex =>
            {
                // See what kind of exception it was to further diagnose
                if(ex is GcmNotificationException)
                {
                    var notificationException = ex as GcmNotificationException;

                    // Deal with the failed notification
                    var gcmNotification = notificationException.Notification;
                    var description = notificationException.Description;

                    Debug.WriteLine($"GCM Notification Failed: ID={gcmNotification.MessageId}, Desc={description}");
                }
                else if(ex is GcmMulticastResultException)
                {
                    var multicastException = ex as GcmMulticastResultException;

                    foreach(var succeededNotification in multicastException.Succeeded)
                    {
                        Debug.WriteLine($"GCM Notification Failed: ID={succeededNotification.MessageId}");
                    }

                    foreach(var failedKvp in multicastException.Failed)
                    {
                        var n = failedKvp.Key;
                        var e = failedKvp.Value;

                        Debug.WriteLine($"GCM Notification Failed: ID={n.MessageId}, Desc={e.Message}");
                    }

                }
                else if(ex is DeviceSubscriptionExpiredException)
                {
                    var expiredException = ex as DeviceSubscriptionExpiredException;

                    var oldId = expiredException.OldSubscriptionId;
                    var newId = expiredException.NewSubscriptionId;

                    Debug.WriteLine($"Device RegistrationId Expired: {oldId}");

                    if(!string.IsNullOrWhiteSpace(newId))
                    {
                        // If this value isn't null, our subscription changed and we should update our database
                        Debug.WriteLine($"Device RegistrationId Changed To: {newId}");
                    }
                }
                else if(ex is RetryAfterException)
                {
                    var retryException = (RetryAfterException)ex;
                    // If you get rate limited, you should stop sending messages until after the RetryAfterUtc date
                    Debug.WriteLine($"GCM Rate Limited, don't send more until after {retryException.RetryAfterUtc}");
                }
                else
                {
                    Debug.WriteLine("GCM Notification Failed for some unknown reason");
                }

                // Mark it as handled
                return true;
            });
        }

        private void InitApnsBroker(GlobalSettings globalSettings, IHostingEnvironment hostingEnvironment)
        {
            if(string.IsNullOrWhiteSpace(globalSettings.Push.ApnsCertificatePassword)
                || string.IsNullOrWhiteSpace(globalSettings.Push.ApnsCertificateThumbprint))
            {
                return;
            }

            var apnsCertificate = CoreHelpers.GetCertificate(globalSettings.Push.ApnsCertificateThumbprint);
            if(apnsCertificate == null)
            {
                return;
            }

            var apnsConfig = new ApnsConfiguration(hostingEnvironment.IsProduction() ?
                ApnsConfiguration.ApnsServerEnvironment.Production : ApnsConfiguration.ApnsServerEnvironment.Sandbox,
                apnsCertificate.RawData, globalSettings.Push.ApnsCertificatePassword);

            _apnsBroker = new ApnsServiceBroker(apnsConfig);
            _apnsBroker.OnNotificationFailed += ApnsBroker_OnNotificationFailed;
            _apnsBroker.OnNotificationSucceeded += (notification) =>
            {
                Debug.WriteLine("Apple Notification Sent!");
            };
            _apnsBroker.Start();

            var feedbackService = new FeedbackService(apnsConfig);
            feedbackService.FeedbackReceived += FeedbackService_FeedbackReceived;
            feedbackService.Check();
        }

        private void ApnsBroker_OnNotificationFailed(ApnsNotification notification, AggregateException exception)
        {
            exception.Handle(ex =>
            {
                // See what kind of exception it was to further diagnose
                if(ex is ApnsNotificationException)
                {
                    var notificationException = ex as ApnsNotificationException;

                    // Deal with the failed notification
                    var apnsNotification = notificationException.Notification;
                    var statusCode = notificationException.ErrorStatusCode;

                    Debug.WriteLine($"Apple Notification Failed: ID={apnsNotification.Identifier}, Code={statusCode}");
                }
                else
                {
                    // Inner exception might hold more useful information like an ApnsConnectionException
                    Debug.WriteLine($"Apple Notification Failed for some unknown reason : {ex.InnerException}");
                }

                // Mark it as handled
                return true;
            });
        }

        private void FeedbackService_FeedbackReceived(string deviceToken, DateTime timestamp)
        {
            // Remove the deviceToken from your database
            // timestamp is the time the token was reported as expired
        }

        private async Task PushToAllUserDevicesAsync(Guid userId, JObject message, IEnumerable<string> tokensToSkip)
        {
            var devices = (await _deviceRepository.GetManyByUserIdAsync(userId))
                .Where(d => !string.IsNullOrWhiteSpace(d.PushToken) && (!tokensToSkip?.Contains(d.PushToken) ?? true));
            if(devices.Count() == 0)
            {
                return;
            }

            if(_apnsBroker != null)
            {
                // Send to each iOS device
                foreach(var device in devices.Where(d => d.Type == DeviceType.iOS))
                {
                    _apnsBroker.QueueNotification(new ApnsNotification
                    {
                        DeviceToken = device.PushToken,
                        Payload = message
                    });
                }
            }

            // Android can send to many devices at once
            var androidDevices = devices.Where(d => d.Type == DeviceType.Android);
            if(_gcmBroker != null && androidDevices.Count() > 0)
            {
                _gcmBroker.QueueNotification(new GcmNotification
                {
                    RegistrationIds = androidDevices.Select(d => d.PushToken).ToList(),
                    Data = message
                });
            }
        }

        private class PushNotification
        {
            public PushType Type { get; set; }
            [JsonProperty(PropertyName = "aps")]
            public AppleData Aps { get; set; }

            public class AppleData
            {
                [JsonProperty(PropertyName = "badge")]
                public dynamic Badge { get; set; } = null;
                [JsonProperty(PropertyName = "alert")]
                public string Alert { get; set; }
                [JsonProperty(PropertyName = "content-available")]
                public int ContentAvailable { get; set; }
            }
        }

        private class SyncCipherPushNotification : PushNotification
        {
            public Guid Id { get; set; }
            public Guid? UserId { get; set; }
            public Guid? OrganizationId { get; set; }
            public DateTime RevisionDate { get; set; }
        }

        private class SyncFolderPushNotification : PushNotification
        {
            public Guid Id { get; set; }
            public Guid UserId { get; set; }
            public DateTime RevisionDate { get; set; }
        }

        private class SyncUserPushNotification : PushNotification
        {
            public Guid UserId { get; set; }
            public DateTime Date { get; set; }
        }
    }
}
