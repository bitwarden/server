using System;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class SmtpMailDeliveryServiceTests
    {
        private readonly SmtpMailDeliveryService _sut;

        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<SmtpMailDeliveryService> _logger;

        public SmtpMailDeliveryServiceTests()
        {
            _globalSettings = new GlobalSettings();
            _logger = Substitute.For<ILogger<SmtpMailDeliveryService>>();

            _globalSettings.Mail.Smtp.Host = "unittests.example.com";
            _globalSettings.Mail.ReplyToEmail = "noreply@unittests.example.com";

            _sut = new SmtpMailDeliveryService(_globalSettings, _logger);
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
