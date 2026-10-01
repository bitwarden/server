using System.Text.Json;
using Bit.Core.Enums;

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
    public PushType Type { get; set; }
    public JsonElement Payload { get; set; }
    public string? ContextId { get; set; }

    /// <summary>
    /// Reads the payload as <typeparamref name="T"/> and returns the notification in the shape
    /// clients receive, or <see langword="null"/> when the payload does not parse as that type.
    /// </summary>
    public OutboundNotification<T>? ToOutbound<T>(JsonSerializerOptions options)
    {
        var payload = Payload.Deserialize<T>(options);
        return payload is null
            ? null
            : new OutboundNotification<T> { Type = Type, Payload = payload, ContextId = ContextId };
    }
}
