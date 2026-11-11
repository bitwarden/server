using Bit.Core.Enums;

namespace Notifications.Test;

public class HubHelpersTests
{
    /// <summary>
    /// <see cref="Bit.Notifications.HubHelpers"/> recognizes auth request responses by the wire value
    /// 16 rather than the enum, so it routes them to the anonymous hub only while the two agree.
    /// </summary>
    [Fact]
    public void AuthRequestResponse_WireValueIsUnchanged()
    {
        Assert.Equal(16, (byte)PushType.AuthRequestResponse);
    }
}
