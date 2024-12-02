using AutoFixture;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using GlobalSettingsCustomization = Bit.Test.Common.AutoFixture.GlobalSettings;

namespace Bit.Core.Platform.Push.Test;

public class MultiServicePushNotificationServiceTests
{
    private readonly MultiServicePushNotificationService _sut;

    private readonly ILogger<MultiServicePushNotificationService> _logger;
    private readonly ILogger<RelayPushNotificationService> _relayLogger;
    private readonly ILogger<NotificationsApiPushNotificationService> _hubLogger;
    private readonly IEnumerable<IPushNotificationService> _services;
    private readonly Settings.GlobalSettings _globalSettings;

    public MultiServicePushNotificationServiceTests()
    {
        _logger = Substitute.For<ILogger<MultiServicePushNotificationService>>();
        _relayLogger = Substitute.For<ILogger<RelayPushNotificationService>>();
        _hubLogger = Substitute.For<ILogger<NotificationsApiPushNotificationService>>();

        var fixture = new Fixture().WithAutoNSubstitutions().Customize(new GlobalSettingsCustomization());
        _services = fixture.CreateMany<IPushNotificationService>();
        _globalSettings = fixture.Create<Settings.GlobalSettings>();

        _sut = new MultiServicePushNotificationService(
            _services,
            _logger,
            _globalSettings
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
