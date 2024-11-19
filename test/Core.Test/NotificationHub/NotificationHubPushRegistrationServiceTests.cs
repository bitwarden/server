using Bit.Core.NotificationHub;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationHub;

public class NotificationHubPushRegistrationServiceTests
{
    private readonly NotificationHubPushRegistrationService _sut;

    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationHubPushRegistrationService> _logger;
    private readonly GlobalSettings _globalSettings;
    private readonly INotificationHubPool _notificationHubPool;

    public NotificationHubPushRegistrationServiceTests()
    {
        _installationDeviceRepository = Substitute.For<IInstallationDeviceRepository>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<NotificationHubPushRegistrationService>>();
        _globalSettings = new GlobalSettings();
        _notificationHubPool = Substitute.For<INotificationHubPool>();

        _sut = new NotificationHubPushRegistrationService(
            _installationDeviceRepository,
            _globalSettings,
            _notificationHubPool,
            _serviceProvider,
            _logger
        );
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact(Skip = "Needs additional work")]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
