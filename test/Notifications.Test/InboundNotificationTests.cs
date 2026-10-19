using Bit.Core.Enums;
using Bit.Notifications;

namespace Notifications.Test;

public class InboundNotificationTests
{
    [Fact]
    public void Type_MatchesPushTypeBackingType()
    {
        var typeProperty = typeof(InboundNotification).GetProperty(nameof(InboundNotification.Type))!;

        Assert.Equal(Enum.GetUnderlyingType(typeof(PushType)), typeProperty.PropertyType);
    }
}
