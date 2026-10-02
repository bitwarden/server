using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;

namespace Bit.Notifications;

/// <summary>
/// A notification received from <see cref="AzureQueueHostedService"/> or <see cref="Controllers.SendController"/>.
/// </summary>
/// <remarks>
/// <para>This type is an envelope containing a serialized notification. <see cref="Type"/> identifies which notfication
/// class has been serialized and stored in <see cref="Payload"/>. The payload contains the information necessary to
/// route the notification to the appropriate place.
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
    /// Reads the payload as <typeparamref name="T"/> and returns the notification in the shape
    /// clients receive, or <see langword="null"/> when the payload does not parse as that type.
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
