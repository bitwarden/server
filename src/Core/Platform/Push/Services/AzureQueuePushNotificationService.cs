#nullable enable
using System.Text.Json;
using Azure.Storage.Queues;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

public class AzureQueuePushNotificationService : IPushEngine
{
    private readonly QueueClient _queueClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AzureQueuePushNotificationService(
        [FromKeyedServices("notifications")] QueueClient queueClient,
        IHttpContextAccessor httpContextAccessor,
        IGlobalSettings globalSettings,
        ILogger<AzureQueuePushNotificationService> logger,
        TimeProvider timeProvider)
    {
        _queueClient = queueClient;
        _httpContextAccessor = httpContextAccessor;
        if (globalSettings.Installation.Id == Guid.Empty)
        {
            logger.LogWarning("Installation ID is not set. Push notifications for installations will not work.");
        }
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
            };

            await SendMessageAsync(type, message, true);
        }
    }

    private async Task SendMessageAsync<T>(PushType type, T payload, bool excludeCurrentContext)
    {
        var contextId = GetContextIdentifier(excludeCurrentContext);
        var message = JsonSerializer.Serialize(new PushNotificationData<T>(type, payload, contextId),
            JsonHelpers.IgnoreWritingNull);
        await _queueClient.SendMessageAsync(message);
    }

    private string? GetContextIdentifier(bool excludeCurrentContext)
    {
        if (!excludeCurrentContext)
        {
            return null;
        }

        var currentContext =
            _httpContextAccessor?.HttpContext?.RequestServices.GetService(typeof(ICurrentContext)) as ICurrentContext;
        return currentContext?.DeviceIdentifier;
    }

    public async Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class
    {
        await SendMessageAsync(pushNotification.Type, pushNotification.Payload, pushNotification.ExcludeCurrentContext);
    }
}
