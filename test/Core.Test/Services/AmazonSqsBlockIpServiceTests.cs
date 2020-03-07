using System;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class AmazonSqsBlockIpServiceTests : IDisposable
    {
        private readonly AmazonSqsBlockIpService _sut;

        private readonly GlobalSettings _globalSettings;

        public AmazonSqsBlockIpServiceTests()
        {
            _globalSettings = new GlobalSettings();

            _sut = new AmazonSqsBlockIpService(_globalSettings);
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
