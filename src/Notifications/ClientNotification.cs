using Bit.Core.Enums;

namespace Bit.Notifications;

/// <summary>
/// A notification as connected clients receive it.
/// </summary>
/// <remarks>
/// These properties are the client-facing wire format. ContractlessStandardResolver, configured in
/// Startup, emits a string-keyed map, so
/// renaming one changes what clients decode, while reordering them changes only the encoded bytes --
/// and so the frames pinned by PushNotificationWireFormatTests -- without affecting a decoder that
/// reads by name.
/// </remarks>
public class ClientNotification<T>
{
    public required PushType Type { get; init; }
    public required T Payload { get; init; }
    public required string? ContextId { get; init; }
}
