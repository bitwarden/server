using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Bit.Core.Enums;
using System.Linq;
using System;

namespace Bit.Core.Services
{
    public class NotificationHubPushRegistrationService : IPushRegistrationService
    {
        private readonly GlobalSettings _globalSettings;
        
        private NotificationHubClient _client = null;
        private DateTime? _clientExpires = null;

        public NotificationHubPushRegistrationService(
            GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public async Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
            string identifier, DeviceType type)
        {
            if(string.IsNullOrWhiteSpace(pushToken))
            {
                return;
            }

            var installation = new Installation
            {
                InstallationId = deviceId,
                PushChannel = pushToken,
                Templates = new Dictionary<string, InstallationTemplate>()
            };

            installation.Tags = new List<string>
            {
                $"userId:{userId}"
            };

            if(!string.IsNullOrWhiteSpace(identifier))
            {
                installation.Tags.Add("deviceIdentifier:" + identifier);
            }

            string payloadTemplate = null, messageTemplate = null, badgeMessageTemplate = null;
            switch(type)
            {
                case DeviceType.Android:
                    payloadTemplate = "{\"data\":{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}}";
                    messageTemplate = "{\"data\":{\"data\":{\"type\":\"#(type)\"}," +
                        "\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}}";

                    installation.Platform = NotificationPlatform.Gcm;
                    break;
                case DeviceType.iOS:
                    payloadTemplate = "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}," +
                        "\"aps\":{\"alert\":null,\"badge\":null,\"content-available\":1}}";
                    messageTemplate = "{\"data\":{\"type\":\"#(type)\"}," +
                        "\"aps\":{\"alert\":\"$(message)\",\"badge\":null,\"content-available\":1}}";
                    badgeMessageTemplate = "{\"data\":{\"type\":\"#(type)\"}," +
                        "\"aps\":{\"alert\":\"$(message)\",\"badge\":\"#(badge)\",\"content-available\":1}}";

                    installation.Platform = NotificationPlatform.Apns;
                    break;
                case DeviceType.AndroidAmazon:
                    payloadTemplate = "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}";
                    messageTemplate = "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}";

                    installation.Platform = NotificationPlatform.Adm;
                    break;
                default:
                    break;
            }

            BuildInstallationTemplate(installation, "payload", payloadTemplate, userId, identifier);
            BuildInstallationTemplate(installation, "message", messageTemplate, userId, identifier);
            BuildInstallationTemplate(installation, "badgeMessage", badgeMessageTemplate ?? messageTemplate,
                userId, identifier);

            await RenewClientAndExecuteAsync(async client =>
                await client.CreateOrUpdateInstallationAsync(installation));
        }

        private void BuildInstallationTemplate(Installation installation, string templateId, string templateBody,
            string userId, string identifier)
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

            if(!string.IsNullOrWhiteSpace(identifier))
            {
                template.Tags.Add($"{fullTemplateId}_deviceIdentifier:{identifier}");
            }

            installation.Templates.Add(fullTemplateId, template);
        }

        public async Task DeleteRegistrationAsync(string deviceId)
        {
            try
            {
                await RenewClientAndExecuteAsync(async client => await client.DeleteInstallationAsync(deviceId));
            }
            catch(Exception e)
            {
                if(e.InnerException == null || !e.InnerException.Message.Contains("(404) Not Found"))
                {
                    throw e;
                }
            }
        }

        public async Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            await PatchTagsForUserDevicesAsync(deviceIds, UpdateOperationType.Add, $"organizationId:{organizationId}");
        }

        public async Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            await PatchTagsForUserDevicesAsync(deviceIds, UpdateOperationType.Remove,
                $"organizationId:{organizationId}");
        }

        private async Task PatchTagsForUserDevicesAsync(IEnumerable<string> deviceIds, UpdateOperationType op,
            string tag)
        {
            if(!deviceIds.Any())
            {
                return;
            }

            var operation = new PartialUpdateOperation
            {
                Operation = op,
                Path = "/tags"
            };

            if(op == UpdateOperationType.Add)
            {
                operation.Value = tag;
            }
            else if(op == UpdateOperationType.Remove)
            {
                operation.Path += $"/{tag}";
            }

            foreach(var id in deviceIds)
            {
                try
                {
                    await RenewClientAndExecuteAsync(async client =>
                        await client.PatchInstallationAsync(id, new List<PartialUpdateOperation> { operation }));
                }
                catch(Exception e)
                {
                    if(e.InnerException == null || !e.InnerException.Message.Contains("(404) Not Found"))
                    {
                        throw e;
                    }
                }
            }
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
