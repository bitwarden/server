using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class NotificationHubPushRegistrationService : IPushRegistrationService
    {
        private readonly NotificationHubClient _client;
        private readonly IDeviceRepository _deviceRepository;

        public NotificationHubPushRegistrationService(
            GlobalSettings globalSettings,
            IDeviceRepository deviceRepository)
        {
            _client = NotificationHubClient.CreateClientFromConnectionString(globalSettings.NotificationHub.ConnectionString,
                globalSettings.NotificationHub.HubName);

            _deviceRepository = deviceRepository;
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
                PushChannel = device.PushToken,
                Templates = new Dictionary<string, InstallationTemplate>()
            };

            installation.Tags = new List<string>
            {
                $"userId:{device.UserId}"
            };

            if(!string.IsNullOrWhiteSpace(device.Identifier))
            {
                installation.Tags.Add("deviceIdentifier:" + device.Identifier);
            }

            string payloadTemplate = null, messageTemplate = null, badgeMessageTemplate = null;
            switch(device.Type)
            {
                case Enums.DeviceType.Android:
                    payloadTemplate = "{\"data\":{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}}";
                    messageTemplate = "{\"data\":{\"data\":{\"type\":\"#(type)\"}," +
                        "\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}}";

                    installation.Platform = NotificationPlatform.Gcm;
                    break;
                case Enums.DeviceType.iOS:
                    payloadTemplate = "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}," +
                        "\"aps\":{\"alert\":null,\"badge\":null,\"content-available\":1}}";
                    messageTemplate = "{\"data\":{\"type\":\"#(type)\"}," +
                        "\"aps\":{\"alert\":\"$(message)\",\"badge\":null,\"content-available\":1}}";
                    badgeMessageTemplate = "{\"data\":{\"type\":\"#(type)\"}," +
                        "\"aps\":{\"alert\":\"$(message)\",\"badge\":\"#(badge)\",\"content-available\":1}}";

                    installation.Platform = NotificationPlatform.Apns;
                    break;
                case Enums.DeviceType.AndroidAmazon:
                    payloadTemplate = "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}";
                    messageTemplate = "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}";

                    installation.Platform = NotificationPlatform.Adm;
                    break;
                default:
                    break;
            }

            BuildInstallationTemplate(installation, "payload", payloadTemplate, device.UserId, device.Identifier);
            BuildInstallationTemplate(installation, "message", messageTemplate, device.UserId, device.Identifier);
            BuildInstallationTemplate(installation, "badgeMessage", badgeMessageTemplate ?? messageTemplate, device.UserId,
                device.Identifier);

            await _client.CreateOrUpdateInstallationAsync(installation);
        }

        private void BuildInstallationTemplate(Installation installation, string templateId, string templateBody,
            Guid userId, string deviceIdentifier)
        {
            if(templateBody == null)
            {
                return;
            }

            var fullTemplateId = $"template:{templateId}";

            var template = new InstallationTemplate
            {
                Body = templateBody,
                Tags = new List<string>
                {
                    fullTemplateId,
                    $"{fullTemplateId}_userId:{userId}"
                }
            };

            if(!string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                template.Tags.Add($"{fullTemplateId}_deviceIdentifier:{deviceIdentifier}");
            }

            installation.Templates.Add(fullTemplateId, template);
        }

        public async Task DeleteRegistrationAsync(Guid deviceId)
        {
            await _client.DeleteInstallationAsync(deviceId.ToString());
        }

        public async Task AddUserRegistrationOrganizationAsync(Guid userId, Guid organizationId)
        {
            await PatchTagsForUserDevicesAsync(userId, UpdateOperationType.Add, $"organizationId:{organizationId}");
        }

        public async Task DeleteUserRegistrationOrganizationAsync(Guid userId, Guid organizationId)
        {
            await PatchTagsForUserDevicesAsync(userId, UpdateOperationType.Remove, $"organizationId:{organizationId}");
        }

        private async Task PatchTagsForUserDevicesAsync(Guid userId, UpdateOperationType op, string tag)
        {
            var devices = await _deviceRepository.GetManyByUserIdAsync(userId);
            var operation = new PartialUpdateOperation
            {
                Operation = op,
                Path = "/tags",
                Value = tag
            };

            foreach(var device in devices)
            {
                await _client.PatchInstallationAsync(device.Id.ToString(), new List<PartialUpdateOperation> { operation });
            }
        }
    }
}
