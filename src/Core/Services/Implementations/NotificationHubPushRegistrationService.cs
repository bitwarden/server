using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class NotificationHubPushRegistrationService : IPushRegistrationService
{
    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationHubPushRegistrationService> _logger;
    private Dictionary<NotificationHubType, NotificationHubClient> _clients = [];

    public NotificationHubPushRegistrationService(
        IInstallationDeviceRepository installationDeviceRepository,
        GlobalSettings globalSettings,
        IServiceProvider serviceProvider,
        ILogger<NotificationHubPushRegistrationService> logger)
    {
        _installationDeviceRepository = installationDeviceRepository;
        _globalSettings = globalSettings;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Is this dirty to do in the ctor?
        void addHub(NotificationHubType type)
        {
            var hubRegistration = globalSettings.NotificationHubs.FirstOrDefault(
                h => h.HubType == type && h.EnableRegistration);
            if (hubRegistration != null)
            {
                var client = NotificationHubClient.CreateClientFromConnectionString(
                    hubRegistration.ConnectionString,
                    hubRegistration.HubName,
                    hubRegistration.EnableSendTracing);
                _clients.Add(type, client);
            }
        }

        addHub(NotificationHubType.General);
        addHub(NotificationHubType.iOS);
        addHub(NotificationHubType.Android);
    }

    public async Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
        string identifier, DeviceType type)
    {
        if (string.IsNullOrWhiteSpace(pushToken))
        {
            return;
        }

        var installation = new Installation
        {
            InstallationId = deviceId,
            PushChannel = pushToken,
            Templates = new Dictionary<string, InstallationTemplate>()
        };

        var clientType = DeviceTypes.ToClientType(type);

        installation.Tags = new List<string> { $"userId:{userId}", $"clientType:{clientType}" };

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            installation.Tags.Add("deviceIdentifier:" + identifier);
        }

        string payloadTemplate = null, messageTemplate = null, badgeMessageTemplate = null;
        switch (type)
        {
            case DeviceType.Android:
                var featureService = _serviceProvider.GetRequiredService<IFeatureService>();
                if (featureService.IsEnabled(FeatureFlagKeys.AnhFcmv1Migration))
                {
                    payloadTemplate = "{\"message\":{\"data\":{\"type\":\"$(type)\",\"payload\":\"$(payload)\"}}}";
                    messageTemplate = "{\"message\":{\"data\":{\"type\":\"$(type)\"}," +
                                      "\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}}";
                    installation.Platform = NotificationPlatform.FcmV1;
                }
                else
                {
                    payloadTemplate = "{\"data\":{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}}";
                    messageTemplate = "{\"data\":{\"data\":{\"type\":\"#(type)\"}," +
                                      "\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}}";
                    installation.Platform = NotificationPlatform.Fcm;
                }

                break;
            case DeviceType.iOS:
                payloadTemplate = "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}," +
                                  "\"aps\":{\"content-available\":1}}";
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

        BuildInstallationTemplate(installation, "payload", payloadTemplate, userId, identifier, clientType);
        BuildInstallationTemplate(installation, "message", messageTemplate, userId, identifier, clientType);
        BuildInstallationTemplate(installation, "badgeMessage", badgeMessageTemplate ?? messageTemplate,
            userId, identifier, clientType);

        await GetClient(type).CreateOrUpdateInstallationAsync(installation);
        if (InstallationDeviceEntity.IsInstallationDeviceId(deviceId))
        {
            await _installationDeviceRepository.UpsertAsync(new InstallationDeviceEntity(deviceId));
        }
    }

    private void BuildInstallationTemplate(Installation installation, string templateId, string templateBody,
        string userId, string identifier, ClientType clientType)
    {
        if (templateBody == null)
        {
            return;
        }

        var fullTemplateId = $"template:{templateId}";

        var template = new InstallationTemplate
        {
            Body = templateBody,
            Tags = new List<string>
            {
                fullTemplateId, $"{fullTemplateId}_userId:{userId}", $"clientType:{clientType}"
            }
        };

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            template.Tags.Add($"{fullTemplateId}_deviceIdentifier:{identifier}");
        }

        installation.Templates.Add(fullTemplateId, template);
    }

    public async Task DeleteRegistrationAsync(string deviceId, DeviceType deviceType)
    {
        try
        {
            await GetClient(deviceType).DeleteInstallationAsync(deviceId);
            if (InstallationDeviceEntity.IsInstallationDeviceId(deviceId))
            {
                await _installationDeviceRepository.DeleteAsync(new InstallationDeviceEntity(deviceId));
            }
        }
        catch (Exception e) when (e.InnerException == null || !e.InnerException.Message.Contains("(404) Not Found"))
        {
            throw;
        }
    }

    public async Task AddUserRegistrationOrganizationAsync(IEnumerable<KeyValuePair<string, DeviceType>> devices,
        string organizationId)
    {
        await PatchTagsForUserDevicesAsync(devices, UpdateOperationType.Add, $"organizationId:{organizationId}");
        if (devices.Any() && InstallationDeviceEntity.IsInstallationDeviceId(devices.First().Key))
        {
            var entities = devices.Select(e => new InstallationDeviceEntity(e.Key));
            await _installationDeviceRepository.UpsertManyAsync(entities.ToList());
        }
    }

    public async Task DeleteUserRegistrationOrganizationAsync(IEnumerable<KeyValuePair<string, DeviceType>> devices,
        string organizationId)
    {
        await PatchTagsForUserDevicesAsync(devices, UpdateOperationType.Remove,
            $"organizationId:{organizationId}");
        if (devices.Any() && InstallationDeviceEntity.IsInstallationDeviceId(devices.First().Key))
        {
            var entities = devices.Select(e => new InstallationDeviceEntity(e.Key));
            await _installationDeviceRepository.UpsertManyAsync(entities.ToList());
        }
    }

    private async Task PatchTagsForUserDevicesAsync(IEnumerable<KeyValuePair<string, DeviceType>> devices,
        UpdateOperationType op,
        string tag)
    {
        if (!devices.Any())
        {
            return;
        }

        var operation = new PartialUpdateOperation { Operation = op, Path = "/tags" };

        if (op == UpdateOperationType.Add)
        {
            operation.Value = tag;
        }
        else if (op == UpdateOperationType.Remove)
        {
            operation.Path += $"/{tag}";
        }

        foreach (var device in devices)
        {
            try
            {
                await GetClient(device.Value)
                    .PatchInstallationAsync(device.Key, new List<PartialUpdateOperation> { operation });
            }
            catch (Exception e) when (e.InnerException == null || !e.InnerException.Message.Contains("(404) Not Found"))
            {
                throw;
            }
        }
    }

    private NotificationHubClient GetClient(DeviceType deviceType)
    {
        var clientType = DeviceTypes.ToClientType(deviceType);

        var hubType = clientType switch
        {
            ClientType.Web => NotificationHubType.GeneralWeb,
            ClientType.Browser => NotificationHubType.GeneralBrowserExtension,
            ClientType.Desktop => NotificationHubType.GeneralDesktop,
            ClientType.Mobile => deviceType switch
            {
                DeviceType.Android => NotificationHubType.Android,
                DeviceType.iOS => NotificationHubType.iOS,
                _ => NotificationHubType.General
            },
            _ => NotificationHubType.General
        };

        if (!_clients.ContainsKey(hubType))
        {
            _logger.LogWarning("No hub client for '{0}'. Using general hub instead.", hubType);
            hubType = NotificationHubType.General;
            if (!_clients.ContainsKey(hubType))
            {
                throw new Exception("No general hub client found.");
            }
        }

        return _clients[hubType];
    }
}
