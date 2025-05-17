#nullable enable

using System.Text.Json;
using Bit.Core.Enums;

namespace Bit.Core.Platform.Push.Internal;

/// <summary>
/// An object encapsulating the information that is available in a notification
/// given to us from a self-hosted installation.
/// </summary>
public class RelayedNotification
{
    /// <inheritdoc cref="PushNotification{T}.Type"/>
    public required PushType Type { get; init; }
    /// <inheritdoc cref="PushNotification{T}.Target"/>
    public required NotificationTarget Target { get; init; }
    /// <inheritdoc cref="PushNotification{T}.TargetId"/>
    public required Guid TargetId { get; init; }
    /// <inheritdoc cref="PushNotification{T}.Payload"/>
    public required JsonElement Payload { get; init; }
    /// <inheritdoc cref="PushNotification{T}.ClientType"/>
    public required ClientType? ClientType { get; init; }
    public required Guid? DeviceId { get; init; }
    public required string? Identifier { get; init; }
}

/// <summary>
/// A service for taking a notification that was relayed to us from a self-hosted installation and
/// will be injested into our infrastructure so that we can get the notification to devices that require
/// cloud interaction.
/// </summary>
/// <remarks>
/// This interface should be treated as internal and not consumed by other teams.
/// </remarks>
public interface IPushRelayer
{
    /// <summary>
    /// Relays a notification that was received from an authenticated installation into our cloud push notification infrastructure.
    /// </summary>
    /// <param name="fromInstallation">The authenticated installation this notification came from.</param>
    /// <param name="relayedNotification">The information received from the self-hosted installation.</param>
    Task RelayAsync(Guid fromInstallation, RelayedNotification relayedNotification);
}
