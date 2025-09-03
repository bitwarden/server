using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

/// <summary>
/// Sends mobile push notifications to the Azure Notification Hub.
/// Used by Cloud-Hosted environments.
/// Received by Firebase for Android or APNS for iOS.
/// </summary>
public class NotificationHubPushEngine : IPushEngine, IPushRelayer
{
    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly bool _enableTracing = false;
    private readonly INotificationHubPool _notificationHubPool;
    private readonly ILogger _logger;

    public NotificationHubPushEngine(
        IInstallationDeviceRepository installationDeviceRepository,
        INotificationHubPool notificationHubPool,
        IHttpContextAccessor httpContextAccessor,
        ILogger<NotificationHubPushEngine> logger,
        IGlobalSettings globalSettings)
    {
        _installationDeviceRepository = installationDeviceRepository;
        _httpContextAccessor = httpContextAccessor;
        _notificationHubPool = notificationHubPool;
        _logger = logger;
        if (globalSettings.Installation.Id == Guid.Empty)
        {
            logger.LogWarning("Installation ID is not set. Push notifications for installations will not work.");
        }
    }

    public async Task PushCipherAsync(Cipher cipher, PushType type, IEnumerable<Guid>? collectionIds)
    {
        if (cipher.OrganizationId.HasValue)
        {
            // We cannot send org pushes since access logic is much more complicated than just the fact that they belong
            // to the organization. Potentially we could blindly send to just users that have the access all permission
            // device registration needs to be more granular to handle that appropriately. A more brute force approach could
            // me to send "full sync" push to all org users, but that has the potential to DDOS the API in bursts.

            // await SendPayloadToOrganizationAsync(cipher.OrganizationId.Value, type, message, true);
        }
        else if (cipher.UserId.HasValue)
        {
            var message = new SyncCipherPushNotification
            {
                Id = cipher.Id,
                UserId = cipher.UserId,
                OrganizationId = cipher.OrganizationId,
                RevisionDate = cipher.RevisionDate,
                CollectionIds = collectionIds,
            };

            await PushAsync(new PushNotification<SyncCipherPushNotification>
            {
                Type = type,
                Target = NotificationTarget.User,
                TargetId = cipher.UserId.Value,
                Payload = message,
                ExcludeCurrentContext = true,
            });
        }
    }

    private string? GetContextIdentifier(bool excludeCurrentContext)
    {
        if (!excludeCurrentContext)
        {
            return null;
        }

        var currentContext =
            _httpContextAccessor.HttpContext?.RequestServices.GetService(typeof(ICurrentContext)) as ICurrentContext;
        return currentContext?.DeviceIdentifier;
    }

    private string BuildTag(string tag, string? identifier, ClientType? clientType)
    {
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            tag += $" && !deviceIdentifier:{SanitizeTagInput(identifier)}";
        }

        if (clientType.HasValue && clientType.Value != ClientType.All)
        {
            tag += $" && clientType:{clientType}";
        }

        return $"({tag})";
    }

    public async Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class
    {
        var initialTag = pushNotification.Target switch
        {
            NotificationTarget.User => $"template:payload_userId:{pushNotification.TargetId}",
            NotificationTarget.Organization => $"template:payload && organizationId:{pushNotification.TargetId}",
            NotificationTarget.Installation => $"template:payload && installationId:{pushNotification.TargetId}",
            _ => throw new InvalidOperationException($"Push notification target '{pushNotification.Target}' is not valid."),
        };

        await PushCoreAsync(
            initialTag,
            GetContextIdentifier(pushNotification.ExcludeCurrentContext),
            pushNotification.Type,
            pushNotification.ClientType,
            pushNotification.Payload
        );
    }

    public async Task RelayAsync(Guid fromInstallation, RelayedNotification relayedNotification)
    {
        // Relayed notifications need identifiers prefixed with the installation they are from and a underscore
        var initialTag = relayedNotification.Target switch
        {
            NotificationTarget.User => $"template:payload_userId:{fromInstallation}_{relayedNotification.TargetId}",
            NotificationTarget.Organization => $"template:payload && organizationId:{fromInstallation}_{relayedNotification.TargetId}",
            NotificationTarget.Installation => $"template:payload && installationId:{fromInstallation}",
            _ => throw new InvalidOperationException($"Invalid Notification target {relayedNotification.Target}"),
        };

        await PushCoreAsync(
            initialTag,
            relayedNotification.Identifier,
            relayedNotification.Type,
            relayedNotification.ClientType,
            relayedNotification.Payload
        );

        if (relayedNotification.DeviceId.HasValue)
        {
            await _installationDeviceRepository.UpsertAsync(
                new InstallationDeviceEntity(fromInstallation, relayedNotification.DeviceId.Value)
            );
        }
        else
        {
            _logger.LogWarning(
                "A related notification of type '{Type}' came through without a device id from installation {Installation}",
                relayedNotification.Type,
                fromInstallation
            );
        }
    }

    private async Task PushCoreAsync<T>(string initialTag, string? contextId, PushType pushType, ClientType? clientType, T payload)
    {
        var finalTag = BuildTag(initialTag, contextId, clientType);

        var results = await _notificationHubPool.AllClients.SendTemplateNotificationAsync(
            new Dictionary<string, string>
            {
                { "type", ((byte)pushType).ToString() },
                { "payload", JsonSerializer.Serialize(payload) },
            },
            finalTag
        );

        if (_enableTracing)
        {
            foreach (var (client, outcome) in results)
            {
                if (!client.EnableTestSend)
                {
                    continue;
                }

                _logger.LogInformation(
                    "Azure Notification Hub Tracking ID: {Id} | {Type} push notification with {Success} successes and {Failure} failures with a payload of {@Payload} and result of {@Results}",
                    outcome.TrackingId, pushType, outcome.Success, outcome.Failure, payload, outcome.Results);
            }
        }
    }

    private string SanitizeTagInput(string input)
    {
        // Only allow a-z, A-Z, 0-9, and special characters -_:
        return Regex.Replace(input, "[^a-zA-Z0-9-_:]", string.Empty);
    }
}
