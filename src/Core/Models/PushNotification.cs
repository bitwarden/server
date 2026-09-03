using System.Text.Json.Serialization;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;

namespace Bit.Core.Models;

/// <summary>
/// The envelope a notification is sent to the Notifications service in.
/// </summary>
/// <remarks>
/// This is the sending half of the contract, so it is deliberately strict. Every property is
/// required, which means the two engines that build one cannot forget to carry something across
/// from the <see cref="Platform.Push.PushNotification{T}"/> they were handed -- including a property
/// added here later, which becomes a compile error at both call sites rather than a silently
/// defaulted field on the wire. The receiving half is more permissive, because a sender deployed
/// before a property existed does not send it at all.
///
/// <para>The nullable properties are omitted from the serialized envelope rather than written as
/// null. That is done per property instead of through a serializer-wide ignore condition so it stays
/// confined to the envelope: the payload is another team's contract and keeps stating its properties
/// explicitly.</para>
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
