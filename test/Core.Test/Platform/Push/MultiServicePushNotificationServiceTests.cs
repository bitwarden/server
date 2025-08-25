using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Push;

public class MultiServicePushNotificationServiceTests
{
    private readonly IPushEngine _fakeEngine1;
    private readonly IPushEngine _fakeEngine2;

    private readonly MultiServicePushNotificationService _sut;

    public MultiServicePushNotificationServiceTests()
    {
        _fakeEngine1 = Substitute.For<IPushEngine>();
        _fakeEngine2 = Substitute.For<IPushEngine>();

        _sut = new MultiServicePushNotificationService(
            [_fakeEngine1, _fakeEngine2],
            NullLogger<MultiServicePushNotificationService>.Instance,
            new GlobalSettings(),
            new FakeTimeProvider()
        );
    }

#if DEBUG // This test requires debug code in the sut to work properly
    [Fact]
    public async Task PushAsync_CallsAllEngines()
    {
        var notification = new PushNotification<object>
        {
            Target = NotificationTarget.User,
            TargetId = Guid.NewGuid(),
            Type = PushType.AuthRequest,
            Payload = new { },
            ExcludeCurrentContext = false,
        };

        await _sut.PushAsync(notification);

        await _fakeEngine1
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<object>>(n => ReferenceEquals(n, notification)));

        await _fakeEngine2
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<object>>(n => ReferenceEquals(n, notification)));
    }

#endif
}
