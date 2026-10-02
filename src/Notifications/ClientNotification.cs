using System.Text.Json;

namespace Bit.Notifications;

/// <summary>
/// A notification for a client application.
/// </summary>
public class OutboundNotification
{
    public required byte Type { get; init; }
    public required JsonElement Payload { get; init; }
    public required string? ContextId { get; init; }
}
