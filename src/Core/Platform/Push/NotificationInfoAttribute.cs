using Bit.Core.Enums;

namespace Bit.Core.Platform.Push;

/// <summary>
/// Used to annotate information about a given <see cref="PushType"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class NotificationInfoAttribute : Attribute
{
    // Once upon a time we can feed this information into a C# analyzer to make sure that we validate
    // the callsites of IPushNotificationService.PushAsync uses the correct payload type for the notification type
    // for now this only exists as forced documentation to teams who create a push type.

    // It's especially on purpose that we allow ourselves to take a type name via just the string,
    // this allows teams to make a push type that is only sent with a payload that exists in a separate assembly than
    // this one.

    public NotificationInfoAttribute(string team, Type payloadType)
        // It should be impossible to reference an unnamed type for an attributes constructor so this assertion should be safe.
        : this(team, payloadType.FullName!)
    {
        Team = team;
    }

    public NotificationInfoAttribute(string team, string payloadTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadTypeName);

        Team = team;
        PayloadTypeName = payloadTypeName;
    }

    /// <summary>
    /// The name of the team that owns this <see cref="PushType"/>.
    /// </summary>
    public string Team { get; }

    /// <summary>
    /// The fully qualified type name of the payload that should be used when sending a notification of this type.
    /// </summary>
    public string PayloadTypeName { get; }
}
