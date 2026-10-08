namespace Bit.Core.Platform.Push.Models;

/// <summary>
/// Payload for <see cref="Enums.PushType.AgentFillApprovalRequest"/> and
/// <see cref="Enums.PushType.AgentFillApprovalResponse"/>. It carries identifiers only; the approval's
/// content stays sealed on the server and is fetched by the receiving device.
/// </summary>
/// <remarks>
/// Lives in Core because <see cref="NotificationInfoAttribute"/> requires the payload type to be referenced from
/// <see cref="Enums.PushType"/>. Owned by the <c>Bit.AgentFill</c> library.
/// </remarks>
public class AgentFillApprovalPushNotification
{
    /// <summary>The approval request ID.</summary>
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
}
