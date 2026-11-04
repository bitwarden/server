using System.Text.Json.Serialization;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;

namespace Bit.Core.Models;

/// <summary>
/// The envelope for a notification sent to the Notifications service.
/// </summary>
/// <remarks>
/// <para>
/// All properties on this type are required so any <see cref="Platform.Push.Internal.IPushEngine"/> implementations
/// fail to compile if any property is forgotten when a <see cref="PushNotification{T}"/> is mapped to this type.
/// </para>
/// <para>
/// Nullable properties are omitted when serialized. The properties of the payload itself are not affected.
/// </para>
/// </remarks>
public class PushNotificationData<T>
{
    /// <inheritdoc cref="Platform.Push.PushNotification{T}.Type"/>
    public required PushType Type { get; init; }

    /// <inheritdoc cref="Platform.Push.PushNotification{T}.Payload"/>
    public required T Payload { get; init; }

    /// <summary>
    /// The device the notification originated from, when it should not be handled there.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string? ContextId { get; init; }

    /// <inheritdoc cref="Platform.Push.PushNotification{T}.Target"/>
    public required NotificationTarget Target { get; init; }

    /// <inheritdoc cref="Platform.Push.PushNotification{T}.TargetId"/>
    public required Guid TargetId { get; init; }

    /// <inheritdoc cref="Platform.Push.PushNotification{T}.ClientType"/>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required ClientType? ClientType { get; init; }
}

public class UserPushNotification
{
    public Guid UserId { get; set; }
    public DateTime Date { get; set; }
}
