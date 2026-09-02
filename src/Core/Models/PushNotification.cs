using Bit.Core.Enums;

namespace Bit.Core.Models;

public class PushNotificationData<T>
{
    public PushNotificationData(PushType type, T payload, string? contextId)
    {
        Type = type;
        Payload = payload;
        ContextId = contextId;
    }

    public PushType Type { get; set; }
    public T Payload { get; set; }
    public string? ContextId { get; set; }
}

public class UserPushNotification
{
    public Guid UserId { get; set; }
    public DateTime Date { get; set; }
}
