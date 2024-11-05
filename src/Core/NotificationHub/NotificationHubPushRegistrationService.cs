using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.NotificationHub;

public class NotificationHubPushRegistrationService : IPushRegistrationService
{
    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly INotificationHubPool _notificationHubPool;
    private readonly IServiceProvider _serviceProvider;

    public NotificationHubPushRegistrationService(
        IInstallationDeviceRepository installationDeviceRepository,
        INotificationHubPool notificationHubPool,
        IServiceProvider serviceProvider)
    {
        _installationDeviceRepository = installationDeviceRepository;
        _notificationHubPool = notificationHubPool;
        _serviceProvider = serviceProvider;
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
                await using (var serviceScope = _serviceProvider.CreateAsyncScope())
                {
                    var featureService = serviceScope.ServiceProvider.GetRequiredService<IFeatureService>();
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

        await ClientFor(GetComb(deviceId)).CreateOrUpdateInstallationAsync(installation);
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

    public async Task DeleteRegistrationAsync(string deviceId)
    {
        try
        {
            await ClientFor(GetComb(deviceId)).DeleteInstallationAsync(deviceId);
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

    public async Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
    {
        await PatchTagsForUserDevicesAsync(deviceIds, UpdateOperationType.Add, $"organizationId:{organizationId}");
        if (deviceIds.Any() && InstallationDeviceEntity.IsInstallationDeviceId(deviceIds.First()))
        {
            var entities = deviceIds.Select(e => new InstallationDeviceEntity(e));
            await _installationDeviceRepository.UpsertManyAsync(entities.ToList());
        }
    }

    public async Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
    {
        await PatchTagsForUserDevicesAsync(deviceIds, UpdateOperationType.Remove,
            $"organizationId:{organizationId}");
        if (deviceIds.Any() && InstallationDeviceEntity.IsInstallationDeviceId(deviceIds.First()))
        {
            var entities = deviceIds.Select(e => new InstallationDeviceEntity(e));
            await _installationDeviceRepository.UpsertManyAsync(entities.ToList());
        }
    }

    private async Task PatchTagsForUserDevicesAsync(IEnumerable<string> deviceIds, UpdateOperationType op,
        string tag)
    {
        if (!deviceIds.Any())
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

        foreach (var deviceId in deviceIds)
        {
            try
            {
                await ClientFor(GetComb(deviceId))
                    .PatchInstallationAsync(deviceId, new List<PartialUpdateOperation> { operation });
            }
            catch (Exception e) when (e.InnerException == null || !e.InnerException.Message.Contains("(404) Not Found"))
            {
                throw;
            }
        }
    }

    private INotificationHubClient ClientFor(Guid deviceId)
    {
        return _notificationHubPool.ClientFor(deviceId);
    }

    private Guid GetComb(string deviceId)
    {
        if (InstallationDeviceEntity.TryParse(deviceId, out var installationDeviceEntity))
        {
            // Strip off the installation id (PartitionId). RowKey is the ID in the Installation's table.
            deviceId = installationDeviceEntity.RowKey;
        }

        if (!Guid.TryParse(deviceId, out var deviceIdGuid))
        {
            throw new Exception($"Invalid device id {deviceId}.");
        }

        return deviceIdGuid;
    }
}
