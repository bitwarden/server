using Bit.Core.Enums;

namespace Bit.Notifications;

/// <summary>
/// A notification for a client application.
/// </summary>
public class OutboundNotification<T>
{
    public required PushType Type { get; init; }
    public required T Payload { get; init; }
    public required string? ContextId { get; init; }
}
