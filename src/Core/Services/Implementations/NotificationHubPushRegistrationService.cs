using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NotificationHubPushRegistrationService : IPushRegistrationService
    {
        private readonly NotificationHubClient _client;

        public NotificationHubPushRegistrationService(GlobalSettings globalSettings)
        {
            _client = NotificationHubClient.CreateClientFromConnectionString(globalSettings.NotificationHub.ConnectionString,
                globalSettings.NotificationHub.HubName);
        }

        public async Task CreateOrUpdateRegistrationAsync(Device device)
        {
            if(string.IsNullOrWhiteSpace(device.PushToken))
            {
                return;
            }

            var installation = new Installation
            {
                InstallationId = device.Id.ToString(),
                PushChannel = device.PushToken
            };

            installation.Tags = new List<string>
            {
                "userId:" + device.UserId.ToString()
            };

            if(!string.IsNullOrWhiteSpace(device.Identifier))
            {
                installation.Tags.Add("identifier:" + device.Identifier);
            }

            switch(device.Type)
            {
                case Enums.DeviceType.Android:
                    installation.Platform = NotificationPlatform.Gcm;
                    break;
                case Enums.DeviceType.iOS:
                    installation.Platform = NotificationPlatform.Apns;
                    break;
                case Enums.DeviceType.AndroidAmazon:
                    installation.Platform = NotificationPlatform.Adm;
                    break;
                default:
                    break;
            }

            await _client.CreateOrUpdateInstallationAsync(installation);
        }

        public async Task DeleteRegistrationAsync(Guid deviceId)
        {
            await _client.DeleteInstallationAsync(deviceId.ToString());
        }
    }
}
