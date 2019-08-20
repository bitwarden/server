using System;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class SendGridMailDeliveryServiceTests
    {
        private readonly SendGridMailDeliveryService _sut;

        private readonly GlobalSettings _globalSettings;

        public SendGridMailDeliveryServiceTests()
        {
            _globalSettings = new GlobalSettings();

            _sut = new SendGridMailDeliveryService(_globalSettings);
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
