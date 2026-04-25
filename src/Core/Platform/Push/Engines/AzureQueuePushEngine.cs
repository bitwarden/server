using System.Text.Json;
using Azure.Storage.Queues;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

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
