using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Extensions.Logging;

namespace Bit.Core.NotificationHub;

#nullable enable

public class NotificationHubPushRegistrationService : IPushRegistrationService
{
    private static readonly JsonSerializerOptions webPushSerializationOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly INotificationHubPool _notificationHubPool;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationHubPushRegistrationService> _logger;

    public NotificationHubPushRegistrationService(
        IInstallationDeviceRepository installationDeviceRepository,
        INotificationHubPool notificationHubPool,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationHubPushRegistrationService> logger)
    {
        _installationDeviceRepository = installationDeviceRepository;
        _notificationHubPool = notificationHubPool;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task CreateOrUpdateRegistrationAsync(PushRegistrationData data, string deviceId, string userId,
        string? identifier, DeviceType type, IEnumerable<string> organizationIds, Guid installationId)
    {
        var orgIds = organizationIds.ToList();
        var clientType = DeviceTypes.ToClientType(type);
        var installation = new Installation
        {
            InstallationId = deviceId,
            PushChannel = data.Token,
            Tags = new List<string>
            {
                $"userId:{userId}",
                $"clientType:{clientType}"
            }.Concat(orgIds.Select(organizationId => $"organizationId:{organizationId}")).ToList(),
            Templates = new Dictionary<string, InstallationTemplate>()
        };

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            installation.Tags.Add("deviceIdentifier:" + identifier);
        }

        if (installationId != Guid.Empty)
        {
            installation.Tags.Add($"installationId:{installationId}");
        }

        if (data.Token != null)
        {
            await CreateOrUpdateMobileRegistrationAsync(installation, userId, identifier, clientType, orgIds, type, installationId);
        }
        else if (data.WebPush != null)
        {
            await CreateOrUpdateWebRegistrationAsync(data.WebPush.Value.Endpoint, data.WebPush.Value.P256dh, data.WebPush.Value.Auth, installation, userId, identifier, clientType, orgIds, installationId);
        }

        if (InstallationDeviceEntity.IsInstallationDeviceId(deviceId))
        {
            await _installationDeviceRepository.UpsertAsync(new InstallationDeviceEntity(deviceId));
        }
    }

    private async Task CreateOrUpdateMobileRegistrationAsync(Installation installation, string userId,
        string? identifier, ClientType clientType, List<string> organizationIds, DeviceType type, Guid installationId)
    {
        if (string.IsNullOrWhiteSpace(installation.PushChannel))
        {
            return;
        }

        switch (type)
        {
            case DeviceType.Android:
                installation.Templates.Add(BuildInstallationTemplate("payload",
                    "{\"message\":{\"data\":{\"type\":\"$(type)\",\"payload\":\"$(payload)\"}}}",
                    userId, identifier, clientType, organizationIds, installationId));
                installation.Templates.Add(BuildInstallationTemplate("message",
                    "{\"message\":{\"data\":{\"type\":\"$(type)\"}," +
                    "\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}}",
                    userId, identifier, clientType, organizationIds, installationId));
                installation.Templates.Add(BuildInstallationTemplate("badgeMessage",
                    "{\"message\":{\"data\":{\"type\":\"$(type)\"}," +
                    "\"notification\":{\"title\":\"$(title)\",\"body\":\"$(message)\"}}}",
                    userId, identifier, clientType, organizationIds, installationId));
                installation.Platform = NotificationPlatform.FcmV1;
                break;
            case DeviceType.iOS:
                installation.Templates.Add(BuildInstallationTemplate("payload",
                    "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}," +
                    "\"aps\":{\"content-available\":1}}",
                    userId, identifier, clientType, organizationIds, installationId));
                installation.Templates.Add(BuildInstallationTemplate("message",
                    "{\"data\":{\"type\":\"#(type)\"}," +
                    "\"aps\":{\"alert\":\"$(message)\",\"badge\":null,\"content-available\":1}}", userId, identifier, clientType, organizationIds, installationId));
                installation.Templates.Add(BuildInstallationTemplate("badgeMessage",
                    "{\"data\":{\"type\":\"#(type)\"}," +
                    "\"aps\":{\"alert\":\"$(message)\",\"badge\":\"#(badge)\",\"content-available\":1}}",
                    userId, identifier, clientType, organizationIds, installationId));
                installation.Platform = NotificationPlatform.Apns;
                break;
            case DeviceType.AndroidAmazon:
                installation.Templates.Add(BuildInstallationTemplate("payload",
                    "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}",
                    userId, identifier, clientType, organizationIds, installationId));
                installation.Templates.Add(BuildInstallationTemplate("message",
                    "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}",
                    userId, identifier, clientType, organizationIds, installationId));
                installation.Templates.Add(BuildInstallationTemplate("badgeMessage",
                    "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}",
                    userId, identifier, clientType, organizationIds, installationId));

                installation.Platform = NotificationPlatform.Adm;
                break;
            default:
                break;
        }

        await ClientFor(GetComb(installation.InstallationId)).CreateOrUpdateInstallationAsync(installation);
    }

    private async Task CreateOrUpdateWebRegistrationAsync(string endpoint, string p256dh, string auth, Installation installation, string userId,
        string? identifier, ClientType clientType, List<string> organizationIds, Guid installationId)
    {
        // The Azure SDK is currently lacking support for web push registrations.
        // We need to use the REST API directly.

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(p256dh) || string.IsNullOrWhiteSpace(auth))
        {
            return;
        }

        installation.Templates.Add(BuildInstallationTemplate("payload",
            "{\"data\":{\"type\":\"#(type)\",\"payload\":\"$(payload)\"}}",
            userId, identifier, clientType, organizationIds, installationId));
        installation.Templates.Add(BuildInstallationTemplate("message",
            "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}",
                userId, identifier, clientType, organizationIds, installationId));
        installation.Templates.Add(BuildInstallationTemplate("badgeMessage",
            "{\"data\":{\"type\":\"#(type)\",\"message\":\"$(message)\"}}",
            userId, identifier, clientType, organizationIds, installationId));

        var content = new
        {
            installationId = installation.InstallationId,
            pushChannel = new
            {
                endpoint,
                p256dh,
                auth
            },
            platform = "browser",
            tags = installation.Tags,
            templates = installation.Templates
        };

        var client = _httpClientFactory.CreateClient("NotificationHub");
        var request = ConnectionFor(GetComb(installation.InstallationId)).CreateRequest(HttpMethod.Put, $"installations/{installation.InstallationId}");
        request.Content = JsonContent.Create(content, new MediaTypeHeaderValue("application/json"), webPushSerializationOptions);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Web push registration failed: {Response}", body);
        }
        else
        {
            _logger.LogInformation("Web push registration success: {Response}", body);
        }
    }

    private static KeyValuePair<string, InstallationTemplate> BuildInstallationTemplate(string templateId, [StringSyntax(StringSyntaxAttribute.Json)] string templateBody,
        string userId, string? identifier, ClientType clientType, List<string> organizationIds, Guid installationId)
    {
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

        foreach (var organizationId in organizationIds)
        {
            template.Tags.Add($"organizationId:{organizationId}");
        }

        if (installationId != Guid.Empty)
        {
            template.Tags.Add($"installationId:{installationId}");
        }

        return new KeyValuePair<string, InstallationTemplate>(fullTemplateId, template);
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

        var operation = new PartialUpdateOperation
        {
            Operation = op,
            Path = "/tags"
        };

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
                await ClientFor(GetComb(deviceId)).PatchInstallationAsync(deviceId, new List<PartialUpdateOperation> { operation });
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

    private NotificationHubConnection ConnectionFor(Guid deviceId)
    {
        return _notificationHubPool.ConnectionFor(deviceId);
    }

    private Guid GetComb(string deviceId)
    {
        var deviceIdString = deviceId;
        InstallationDeviceEntity installationDeviceEntity;
        Guid deviceIdGuid;
        if (InstallationDeviceEntity.TryParse(deviceIdString, out installationDeviceEntity))
        {
            // Strip off the installation id (PartitionId). RowKey is the ID in the Installation's table.
            deviceIdString = installationDeviceEntity.RowKey;
        }

        if (Guid.TryParse(deviceIdString, out deviceIdGuid))
        {
        }
        else
        {
            throw new Exception($"Invalid device id {deviceId}.");
        }
        return deviceIdGuid;
    }
}
