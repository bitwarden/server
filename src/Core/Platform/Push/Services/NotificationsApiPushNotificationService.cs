#nullable enable
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

// This service is not in the `Internal` namespace because it has direct external references.
namespace Bit.Core.Platform.Push;

/// <summary>
/// Sends non-mobile push notifications to the Azure Queue Api, later received by Notifications Api.
/// Used by Cloud-Hosted environments.
/// Received by AzureQueueHostedService message receiver in Notifications project.
/// </summary>
public class NotificationsApiPushNotificationService : BaseIdentityClientService, IPushEngine
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public NotificationsApiPushNotificationService(
        IHttpClientFactory httpFactory,
        GlobalSettings globalSettings,
        IHttpContextAccessor httpContextAccessor,
        ILogger<NotificationsApiPushNotificationService> logger)
        : base(
            httpFactory,
            globalSettings.BaseServiceUri.InternalNotifications,
            globalSettings.BaseServiceUri.InternalIdentity,
            "internal",
            $"internal.{globalSettings.ProjectName}",
            globalSettings.InternalIdentityKey,
            logger)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task PushCipherAsync(Cipher cipher, PushType type, IEnumerable<Guid>? collectionIds)
    {
        if (cipher.OrganizationId.HasValue)
        {
            var message = new SyncCipherPushNotification
            {
                Id = cipher.Id,
                OrganizationId = cipher.OrganizationId,
                RevisionDate = cipher.RevisionDate,
                CollectionIds = collectionIds,
            };

            await SendMessageAsync(type, message, true);
        }
        else if (cipher.UserId.HasValue)
        {
            var message = new SyncCipherPushNotification
            {
                Id = cipher.Id,
                UserId = cipher.UserId,
                RevisionDate = cipher.RevisionDate,
                CollectionIds = collectionIds,
            };

            await SendMessageAsync(type, message, true);
        }
    }

    private async Task SendMessageAsync<T>(PushType type, T payload, bool excludeCurrentContext)
    {
        var contextId = GetContextIdentifier(excludeCurrentContext);
        var request = new PushNotificationData<T>(type, payload, contextId);
        await SendAsync(HttpMethod.Post, "send", request);
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

    public async Task PushAsync<T>(PushNotification<T> pushNotification) where T : class
    {
        await SendMessageAsync(pushNotification.Type, pushNotification.Payload, pushNotification.ExcludeCurrentContext);
    }
}
