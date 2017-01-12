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
using Bit.Core.Domains;
using Bit.Core.Enums;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Bit.Core.Utilities;

namespace Bit.Core.Services
{
    public class PushSharpPushService : IPushService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILogger<IPushService> _logger;
        private readonly CurrentContext _currentContext;
        private GcmServiceBroker _gcmBroker;
        private ApnsServiceBroker _apnsBroker;

        public PushSharpPushService(
            IDeviceRepository deviceRepository,
            ILogger<IPushService> logger,
            CurrentContext currentContext,
            IHostingEnvironment hostingEnvironment,
            GlobalSettings globalSettings)
        {
            _deviceRepository = deviceRepository;
            _logger = logger;
            _currentContext = currentContext;

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
                case CipherType.Folder:
                    await PushCipherAsync(cipher, PushType.SyncFolderDelete);
                    break;
                case CipherType.Login:
                    await PushCipherAsync(cipher, PushType.SyncLoginDelete);
                    break;
                default:
                    break;
            }
        }

        private async Task PushCipherAsync(Cipher cipher, PushType type)
        {
            var message = new SyncCipherPushNotification
            {
                Type = type,
                Id = cipher.Id,
                UserId = cipher.UserId,
                RevisionDate = cipher.RevisionDate,
                Aps = new PushNotification.AppleData { ContentAvailable = 1 }
            };

            var excludedTokens = new List<string>();
            if(!string.IsNullOrWhiteSpace(_currentContext.DeviceIdentifier))
            {
                excludedTokens.Add(_currentContext.DeviceIdentifier);
            }

            await PushToAllUserDevicesAsync(cipher.UserId, JObject.FromObject(message), excludedTokens);
        }

        public async Task PushSyncCiphersAsync(Guid userId)
        {
            var message = new SyncCiphersPushNotification
            {
                Type = PushType.SyncCiphers,
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

        private abstract class SyncPushNotification : PushNotification
        {
            public Guid UserId { get; set; }
        }

        private class SyncCipherPushNotification : SyncPushNotification
        {
            public Guid Id { get; set; }
            public DateTime RevisionDate { get; set; }
        }

        private class SyncCiphersPushNotification : SyncPushNotification
        {
            public DateTime Date { get; set; }
        }
    }
}
