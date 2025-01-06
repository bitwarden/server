using Bit.Core.NotificationHub;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationHub;

public class NotificationHubPushNotificationServiceTests
{
    private readonly NotificationHubPushNotificationService _sut;

    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly INotificationHubPool _notificationHubPool;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<NotificationsApiPushNotificationService> _logger;

    public NotificationHubPushNotificationServiceTests()
    {
        _installationDeviceRepository = Substitute.For<IInstallationDeviceRepository>();
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _notificationHubPool = Substitute.For<INotificationHubPool>();
        _logger = Substitute.For<ILogger<NotificationsApiPushNotificationService>>();

        _sut = new NotificationHubPushNotificationService(
            _installationDeviceRepository,
            _notificationHubPool,
            _httpContextAccessor,
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
