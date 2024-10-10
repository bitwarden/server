using AutoFixture;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class MultiServicePushNotificationServiceTests
{
    private readonly MultiServicePushNotificationService _sut;

    private readonly ILogger<MultiServicePushNotificationService> _logger;
    private readonly ILogger<RelayPushNotificationService> _relayLogger;
    private readonly ILogger<NotificationsApiPushNotificationService> _hubLogger;
    private readonly IEnumerable<IPushNotificationService> _services;

    public MultiServicePushNotificationServiceTests()
    {
        _logger = Substitute.For<ILogger<MultiServicePushNotificationService>>();
        _relayLogger = Substitute.For<ILogger<RelayPushNotificationService>>();
        _hubLogger = Substitute.For<ILogger<NotificationsApiPushNotificationService>>();
        _services = new Fixture().WithAutoNSubstitutions().CreateMany<IPushNotificationService>();

        _sut = new MultiServicePushNotificationService(
            _services,
            _logger
        );
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
