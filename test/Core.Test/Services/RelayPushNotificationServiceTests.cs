using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class RelayPushNotificationServiceTests
    {
        private readonly RelayPushNotificationService _sut;

        private readonly IHttpClientFactory _httpFactory;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RelayPushNotificationServiceTests()
        {
            _httpFactory = Substitute.For<IHttpClientFactory>();
            _deviceRepository = Substitute.For<IDeviceRepository>();
            _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

            _sut = new RelayPushNotificationService(
                _httpFactory,
                _deviceRepository,
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
