using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;

namespace Bit.Notifications;

/// <summary>
/// A notification received from <see cref="AzureQueueHostedService"/> or <see cref="Controllers.SendController"/>.
/// </summary>
/// <remarks>
/// <para>This type is an envelope containing a serialized notification. <see cref="Type"/> identifies which notification
/// class has been serialized and stored in <see cref="Payload"/>. Routing comes from the envelope's
/// <see cref="Target"/>, <see cref="TargetId"/> and <see cref="ClientType"/>.
/// </para>
/// </remarks>
public class InboundNotification
{
    /// <summary>
    /// Backing type for the PushType enum.
    /// </summary>
    public required byte Type { get; set; }
    public required JsonElement Payload { get; set; }
    public string? ContextId { get; set; }
    public required NotificationTarget Target { get; init; }
    public required Guid TargetId { get; init; }
    public ClientType? ClientType { get; init; }

    /// <summary>
    /// Returns the notification in the shape clients receive, with the envelope's routing fields
    /// removed and the payload passed through unchanged.
    /// </summary>
    public OutboundNotification ToOutbound()
    {
        return new OutboundNotification
        {
            Type = Type,
            Payload = Payload,
            ContextId = ContextId,
        };
    }
}
