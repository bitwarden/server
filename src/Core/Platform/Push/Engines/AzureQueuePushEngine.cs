using System.Text.Json;
using Azure.Storage.Queues;
using Bit.Core.Context;
using Bit.Core.Models;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

/// <summary>
/// Sends notifications to the Notifications service by writing them to the notifications Azure
/// Queue, where AzureQueueHostedService dequeues them and fans them out over SignalR. Registered for
/// cloud-hosted installations that have a notifications queue configured; the self-hosted equivalent
/// is <see cref="NotificationsApiPushEngine"/>.
/// </summary>
/// <remarks>
/// Every notification is written, whatever client type it is bound for; this engine filters nothing.
/// Which connections receive one is decided by the Notifications service, and a mobile app holding a
/// SignalR connection is delivered to like any other client.
/// </remarks>
public class AzureQueuePushEngine : IPushEngine
{
    private readonly QueueClient _queueClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AzureQueuePushEngine(
        [FromKeyedServices("notifications")] QueueClient queueClient,
        IHttpContextAccessor httpContextAccessor,
        IGlobalSettings globalSettings,
        ILogger<AzureQueuePushEngine> logger)
    {
        _queueClient = queueClient;
        _httpContextAccessor = httpContextAccessor;
        if (globalSettings.Installation.Id == Guid.Empty)
        {
            logger.LogWarning("Installation ID is not set. Push notifications for installations will not work.");
        }
    }

    public async Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class
    {
        var message = JsonSerializer.Serialize(new PushNotificationData<T>
        {
            Type = pushNotification.Type,
            Payload = pushNotification.Payload,
            ContextId = GetContextIdentifier(pushNotification.ExcludeCurrentContext),
            Target = pushNotification.Target,
            TargetId = pushNotification.TargetId,
            ClientType = pushNotification.ClientType,
        }, JsonHelpers.Default);
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
}
