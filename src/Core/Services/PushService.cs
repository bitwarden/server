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
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Domains;
using Bit.Core.Enums;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services
{
    public class PushService : IPushService
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly ILogger<IPushService> _logger;
        private GcmServiceBroker _gcmBroker;
        private ApnsServiceBroker _apnsBroker;

        public PushService(
            IDeviceRepository deviceRepository,
            ILogger<IPushService> logger,
            IHostingEnvironment hostingEnvironment,
            GlobalSettings globalSettings)
        {
            _deviceRepository = deviceRepository;
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
                case CipherType.Folder:
                    await PushCipherAsync(cipher, PushType.SyncFolderDelete);
                    break;
                case CipherType.Site:
                    await PushCipherAsync(cipher, PushType.SyncSiteDelete);
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

            await PushToAllUserDevicesAsync(cipher.UserId, JObject.FromObject(message));
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

            await PushToAllUserDevicesAsync(userId, new JObject(message));
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
                Console.WriteLine("GCM Notification Sent!");
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

                    Console.WriteLine($"GCM Notification Failed: ID={gcmNotification.MessageId}, Desc={description}");
                }
                else if(ex is GcmMulticastResultException)
                {
                    var multicastException = ex as GcmMulticastResultException;

                    foreach(var succeededNotification in multicastException.Succeeded)
                    {
                        Console.WriteLine($"GCM Notification Failed: ID={succeededNotification.MessageId}");
                    }

                    foreach(var failedKvp in multicastException.Failed)
                    {
                        var n = failedKvp.Key;
                        var e = failedKvp.Value;

                        Console.WriteLine($"GCM Notification Failed: ID={n.MessageId}, Desc={e.Message}");
                    }

                }
                else if(ex is DeviceSubscriptionExpiredException)
                {
                    var expiredException = ex as DeviceSubscriptionExpiredException;

                    var oldId = expiredException.OldSubscriptionId;
                    var newId = expiredException.NewSubscriptionId;

                    Console.WriteLine($"Device RegistrationId Expired: {oldId}");

                    if(!string.IsNullOrWhiteSpace(newId))
                    {
                        // If this value isn't null, our subscription changed and we should update our database
                        Console.WriteLine($"Device RegistrationId Changed To: {newId}");
                    }
                }
                else if(ex is RetryAfterException)
                {
                    var retryException = (RetryAfterException)ex;
                    // If you get rate limited, you should stop sending messages until after the RetryAfterUtc date
                    Console.WriteLine($"GCM Rate Limited, don't send more until after {retryException.RetryAfterUtc}");
                }
                else
                {
                    Console.WriteLine("GCM Notification Failed for some unknown reason");
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

            var apnsCertificate = GetCertificate(globalSettings.Push.ApnsCertificateThumbprint);
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
                Console.WriteLine("Apple Notification Sent!");
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

                    Console.WriteLine($"Apple Notification Failed: ID={apnsNotification.Identifier}, Code={statusCode}");
                }
                else
                {
                    // Inner exception might hold more useful information like an ApnsConnectionException
                    Console.WriteLine($"Apple Notification Failed for some unknown reason : {ex.InnerException}");
                }

                // Mark it as handled
                return true;
            });
        }

        private X509Certificate2 GetCertificate(string thumbprint)
        {
            // Clean possible garbage characters from thumbprint copy/paste
            // ref http://stackoverflow.com/questions/8448147/problems-with-x509store-certificates-find-findbythumbprint
            thumbprint = Regex.Replace(thumbprint, @"[^\da-zA-z]", string.Empty).ToUpper();

            X509Certificate2 cert = null;
            var certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            certStore.Open(OpenFlags.ReadOnly);
            var certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if(certCollection.Count > 0)
            {
                cert = certCollection[0];
            }
            certStore.Close();
            return cert;
        }

        private void FeedbackService_FeedbackReceived(string deviceToken, DateTime timestamp)
        {
            // Remove the deviceToken from your database
            // timestamp is the time the token was reported as expired
        }

        private async Task PushToAllUserDevicesAsync(Guid userId, JObject message)
        {
            var devices = (await _deviceRepository.GetManyByUserIdAsync(userId)).Where(d => !string.IsNullOrWhiteSpace(d.PushToken));
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
