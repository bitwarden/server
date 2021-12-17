using System;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class NotificationHubPushNotificationServiceTests
    {
        private readonly NotificationHubPushNotificationService _sut;

        private readonly IInstallationDeviceRepository _installationDeviceRepository;
        private readonly GlobalSettings _globalSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NotificationHubPushNotificationServiceTests()
        {
            _installationDeviceRepository = Substitute.For<IInstallationDeviceRepository>();
            _globalSettings = new GlobalSettings();
            _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

            _sut = new NotificationHubPushNotificationService(
                _installationDeviceRepository,
                _globalSettings,
                _httpContextAccessor
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
}
