using Bit.Core.Context;
using Bit.Core.Models;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

/// <summary>
/// Sends notifications to the Notifications service by posting them to its <c>/send</c> endpoint,
/// where SendController receives them and fans them out over SignalR. Registered for self-hosted
/// installations that have an internal identity key and a notifications URI configured; the cloud
/// equivalent is <see cref="AzureQueuePushEngine"/>.
/// </summary>
/// <remarks>
/// Like <see cref="AzureQueuePushEngine"/>, this sends every notification whatever client type it is
/// bound for, and filters nothing.
/// </remarks>
public class NotificationsApiPushEngine : BaseIdentityClientService, IPushEngine
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public NotificationsApiPushEngine(
        IHttpClientFactory httpFactory,
        GlobalSettings globalSettings,
        IHttpContextAccessor httpContextAccessor,
        ILogger<NotificationsApiPushEngine> logger)
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

    public async Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class
    {
        var request = new PushNotificationData<T>
        {
            Type = pushNotification.Type,
            Payload = pushNotification.Payload,
            ContextId = GetContextIdentifier(pushNotification.ExcludeCurrentContext),
            Target = pushNotification.Target,
            TargetId = pushNotification.TargetId,
            ClientType = pushNotification.ClientType,
        };
        // PascalCase, matching what AzureQueuePushEngine writes, so both ingresses of the
        // Notifications service carry the same payload shape.
        await SendAsync(HttpMethod.Post, "send", request, JsonHelpers.Default);
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
}
