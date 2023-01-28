using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class MultiServicePushNotificationServiceTests
{
    private readonly MultiServicePushNotificationService _sut;

    private readonly IHttpClientFactory _httpFactory;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MultiServicePushNotificationService> _logger;
    private readonly ILogger<RelayPushNotificationService> _relayLogger;
    private readonly ILogger<NotificationsApiPushNotificationService> _hubLogger;

    public MultiServicePushNotificationServiceTests()
    {
        _httpFactory = Substitute.For<IHttpClientFactory>();
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _installationDeviceRepository = Substitute.For<IInstallationDeviceRepository>();
        _globalSettings = new GlobalSettings();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _logger = Substitute.For<ILogger<MultiServicePushNotificationService>>();
        _relayLogger = Substitute.For<ILogger<RelayPushNotificationService>>();
        _hubLogger = Substitute.For<ILogger<NotificationsApiPushNotificationService>>();

        _sut = new MultiServicePushNotificationService(
            _httpFactory,
            _deviceRepository,
            _installationDeviceRepository,
            _globalSettings,
            _httpContextAccessor,
            _logger,
            _relayLogger,
            _hubLogger
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
