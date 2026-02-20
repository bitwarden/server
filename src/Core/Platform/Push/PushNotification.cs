using Bit.Core.Enums;

namespace Bit.Core.Platform.Push;

/// <summary>
/// An object containing all the information required for getting a notification
/// to an end users device and the information you want available to that device.
/// </summary>
/// <typeparam name="T">The type of the payload. This type is expected to be able to be roundtripped as JSON.</typeparam>
public record PushNotification<T>
    where T : class
{
    /// <summary>
    /// The <see cref="PushType"/> to be associated with the notification. This is used to route
    /// the notification to the correct handler on the client side. Be sure to use the correct payload
    /// type for the associated <see cref="PushType"/>. 
    /// </summary>
    public required PushType Type { get; init; }

    /// <summary>
    /// The target entity type for the notification.
    /// </summary>
    /// <remarks>
    /// When the target type is <see cref="NotificationTarget.User"/> the <see cref="TargetId"/> 
    /// property is expected to be a users ID. When it is <see cref="NotificationTarget.Organization"/>
    /// it should be an organizations id. When it is a <see cref="NotificationTarget.Installation"/> 
    /// it should be an installation id.
    /// </remarks>
    public required NotificationTarget Target { get; init; }

    /// <summary>
    /// The indentifier for the given <see cref="Target"/>.
    /// </summary>
    public required Guid TargetId { get; init; }

    /// <summary>
    /// The payload to be sent with the notification. This object will be JSON serialized.
    /// </summary>
    public required T Payload { get; init; }

    /// <summary>
    /// When <see langword="true"/> the notification will not include the current context identifier on it, this
    /// means that the notification may get handled on the device that this notification could have originated from.
    /// </summary>
    public required bool ExcludeCurrentContext { get; init; }

    /// <summary>
    /// The type of clients the notification should be sent to, if <see langword="null"/> then 
    /// <see cref="ClientType.All"/> is inferred.
    /// </summary>
    public ClientType? ClientType { get; init; }

    internal Guid? GetTargetWhen(NotificationTarget notificationTarget)
    {
        return Target == notificationTarget ? TargetId : null;
    }
}
