using System.Diagnostics;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public class HubHelpers
{
    private static readonly string _receiveMessageMethod = "ReceiveMessage";

    /// <summary>
    /// The wire value of <c>PushType.AuthRequestResponse</c>, kept as a literal so this project does
    /// not depend on the enum.
    /// </summary>
    private const byte AuthRequestResponseType = 16;

    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly IHubContext<AnonymousNotificationsHub> _anonymousHubContext;
    private readonly ILogger<HubHelpers> _logger;

    public HubHelpers(IHubContext<NotificationsHub> hubContext,
        IHubContext<AnonymousNotificationsHub> anonymousHubContext,
        ILogger<HubHelpers> logger)
    {
        _hubContext = hubContext;
        _anonymousHubContext = anonymousHubContext;
        _logger = logger;
    }

    public async Task SendNotificationToHubAsync(InboundNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending notification: {NotificationType}", notification.Type);

        // An auth request response goes to the device that is waiting to log in, which is not
        // authenticated yet. It listens on the anonymous hub in a group named after the auth
        // request id, so the envelope's target is not used and the id is read from the payload.
        if (notification.Type == AuthRequestResponseType)
        {
            string? authRequestId = null;
            if (notification.Payload.ValueKind == JsonValueKind.Object
                && notification.Payload.TryGetProperty("Id", out var idProperty)
                && idProperty.ValueKind == JsonValueKind.String)
            {
                authRequestId = idProperty.GetString();
            }

            if (string.IsNullOrEmpty(authRequestId))
            {
                const string AuthRequestResponseMessage = "An AuthRequestResponse notification was sent with a null Id in its payload, it will not be able to be delivered.";
                Debug.Fail(AuthRequestResponseMessage);
                _logger.LogError(AuthRequestResponseMessage);
                return;
            }
            await _anonymousHubContext.Clients
                .Group(authRequestId)
                .SendAsync("AuthRequestResponseRecieved", notification.ToOutbound(), cancellationToken);
            return;
        }

        switch (notification.Target)
        {
            case NotificationTarget.User:
                if (notification.ClientType is { } clientType && clientType != ClientType.All)
                {
                    await _hubContext.Clients
                        .Group(NotificationsHub.GetUserGroup(notification.TargetId, clientType))
                        .SendAsync(_receiveMessageMethod, notification.ToOutbound(), cancellationToken);
                }
                else
                {
                    await _hubContext.Clients.User(notification.TargetId.ToString())
                        .SendAsync(_receiveMessageMethod, notification.ToOutbound(), cancellationToken);
                }
                break;
            case NotificationTarget.Organization:
                await _hubContext.Clients
                    .Group(NotificationsHub.GetOrganizationGroup(notification.TargetId, notification.ClientType))
                    .SendAsync(_receiveMessageMethod, notification.ToOutbound(), cancellationToken);
                break;
            case NotificationTarget.Installation:
                await _hubContext.Clients
                    .Group(NotificationsHub.GetInstallationGroup(notification.TargetId, notification.ClientType))
                    .SendAsync(_receiveMessageMethod, notification.ToOutbound(), cancellationToken);
                break;
            default:
                Debug.Fail("A new notification target is not handled in the HubHelpers");
                _logger.LogError("A notification with an unsupported target was sent {Target} {TargetId} {PushType} {ContextId}", notification.Target, notification.TargetId, notification.Type, notification.ContextId);
                break;
        }
    }
}
