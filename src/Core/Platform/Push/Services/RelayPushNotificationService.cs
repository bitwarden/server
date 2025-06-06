#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.IdentityServer;
using Bit.Core.Models;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

/// <summary>
/// Sends mobile push notifications to the Bitwarden Cloud API, then relayed to Azure Notification Hub.
/// Used by Self-Hosted environments.
/// Received by PushController endpoint in Api project.
/// </summary>
public class RelayPushNotificationService : BaseIdentityClientService, IPushEngine
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;


    public RelayPushNotificationService(
        IHttpClientFactory httpFactory,
        IDeviceRepository deviceRepository,
        GlobalSettings globalSettings,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RelayPushNotificationService> logger)
        : base(
            httpFactory,
            globalSettings.PushRelayBaseUri,
            globalSettings.Installation.IdentityUri,
            ApiScopes.ApiPush,
            $"installation.{globalSettings.Installation.Id}",
            globalSettings.Installation.Key,
            logger)
    {
        _deviceRepository = deviceRepository;
        _httpContextAccessor = httpContextAccessor;
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

    public async Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class
    {
        var deviceIdentifier = _httpContextAccessor.HttpContext
            ?.RequestServices.GetService<ICurrentContext>()
            ?.DeviceIdentifier;

        Guid? deviceId = null;

        if (!string.IsNullOrEmpty(deviceIdentifier))
        {
            var device = await _deviceRepository.GetByIdentifierAsync(deviceIdentifier);
            deviceId = device?.Id;
        }

        var payload = new PushSendRequestModel<T>
        {
            Type = pushNotification.Type,
            UserId = pushNotification.GetTargetWhen(NotificationTarget.User),
            OrganizationId = pushNotification.GetTargetWhen(NotificationTarget.Organization),
            InstallationId = pushNotification.GetTargetWhen(NotificationTarget.Installation),
            Payload = pushNotification.Payload,
            Identifier = pushNotification.ExcludeCurrentContext ? deviceIdentifier : null,
            // We set the device id regardless of if they want to exclude the current context or not
            DeviceId = deviceId,
            ClientType = pushNotification.ClientType,
        };

        await SendAsync(HttpMethod.Post, "push/send", payload);
    }
}
