using System;
using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class AmazonSesMailDeliveryServiceTests : IDisposable
    {
        private readonly AmazonSesMailDeliveryService _sut;

        private readonly GlobalSettings _globalSettings;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<AmazonSesMailDeliveryService> _logger;

        public AmazonSesMailDeliveryServiceTests()
        {
            _globalSettings = new GlobalSettings();
            _hostingEnvironment = Substitute.For<IWebHostEnvironment>();
            _logger = Substitute.For<ILogger<AmazonSesMailDeliveryService>>();
            _sut = new AmazonSesMailDeliveryService(
                _globalSettings,
                _hostingEnvironment,
                _logger
            );
        }

        public void Dispose()
        {
            _sut?.Dispose();
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
