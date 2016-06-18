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

namespace Bit.Core.Services
{
    public class PushService : IPushService
    {
        private readonly IDeviceRepository _deviceRepository;
        private GcmServiceBroker _gcmBroker;
        private ApnsServiceBroker _apnsBroker;

        public PushService(
            IDeviceRepository deviceRepository,
            IHostingEnvironment hostingEnvironment,
            GlobalSettings globalSettings)
        {
            _deviceRepository = deviceRepository;

            InitGcmBroker(globalSettings);
            InitApnsBroker(globalSettings, hostingEnvironment);
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
            var devices = (await _deviceRepository.GetManyByUserIdAsync(userId)).Where(d => d.PushToken != null);
            if(devices.Count() == 0)
            {
                return;
            }

            if(_apnsBroker != null)
            {
                // Send to each iOS device
                foreach(var device in devices.Where(d => d.Type == Enums.DeviceType.iOS && d.PushToken != null))
                {
                    _apnsBroker.QueueNotification(new ApnsNotification
                    {
                        DeviceToken = device.PushToken,
                        Payload = message
                    });
                }
            }

            // Android can send to many devices at once
            if(_gcmBroker != null && devices.Any(d => d.Type == Enums.DeviceType.Android))
            {
                _gcmBroker.QueueNotification(new GcmNotification
                {
                    RegistrationIds = devices.Where(d => d.Type == Enums.DeviceType.Android && d.PushToken != null)
                        .Select(d => d.PushToken).ToList(),
                    Data = message
                });
            }
        }
    }
}
