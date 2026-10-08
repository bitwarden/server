using Bit.AgentFill.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Models;

namespace Bit.AgentFill;

internal static class PushNotificationServiceAgentFillExtensions
{
    /// <summary>Tells the user's phones that a new approval request is waiting. The payload holds IDs only.</summary>
    internal static Task PushAgentFillApprovalRequestAsync(this IPushNotificationService service,
        AgentFillApprovalRequest request)
        => service.PushAsync(new PushNotification<AgentFillApprovalPushNotification>
        {
            Type = PushType.AgentFillApprovalRequest,
            Target = NotificationTarget.User,
            TargetId = request.UserId,
            ClientType = ClientType.Mobile,
            Payload = new AgentFillApprovalPushNotification { Id = request.Id, UserId = request.UserId },
            ExcludeCurrentContext = false,
        });

    /// <summary>
    /// Tells the user's desktop apps that an approval request was answered, except the one that answered it.
    /// The payload holds IDs only.
    /// </summary>
    internal static Task PushAgentFillApprovalResponseAsync(this IPushNotificationService service,
        AgentFillApprovalRequest request)
        => service.PushAsync(new PushNotification<AgentFillApprovalPushNotification>
        {
            Type = PushType.AgentFillApprovalResponse,
            Target = NotificationTarget.User,
            TargetId = request.UserId,
            ClientType = ClientType.Desktop,
            Payload = new AgentFillApprovalPushNotification { Id = request.Id, UserId = request.UserId },
            ExcludeCurrentContext = true,
        });
}
