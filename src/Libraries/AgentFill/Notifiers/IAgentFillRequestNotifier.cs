using Bit.AgentFill.Entities;
using Bit.Core.Platform.Push;

namespace Bit.AgentFill.Notifiers;

/// <summary>Tells the user's phones that a new approval request is waiting.</summary>
internal interface IAgentFillRequestNotifier
{
    Task NotifyAsync(AgentFillApprovalRequest request);
}

/// <summary>The default notifier: sends the push through <see cref="IPushNotificationService"/>.</summary>
internal sealed class PushAgentFillRequestNotifier(IPushNotificationService pushNotificationService)
    : IAgentFillRequestNotifier
{
    public Task NotifyAsync(AgentFillApprovalRequest request)
        => pushNotificationService.PushAgentFillApprovalRequestAsync(request);
}
