using System;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class HandlebarsMailServiceTests
    {
        private readonly HandlebarsMailService _sut;

        private readonly GlobalSettings _globalSettings;
        private readonly IMailDeliveryService _mailDeliveryService;

        public HandlebarsMailServiceTests()
        {
            _globalSettings = new GlobalSettings();
            _mailDeliveryService = Substitute.For<IMailDeliveryService>();

            _sut = new HandlebarsMailService(
                _globalSettings,
                _mailDeliveryService
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
}
