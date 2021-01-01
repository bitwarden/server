using System;
using Bit.Core.Services;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class LocalSendStorageServiceTests
    {
        private readonly LocalSendStorageService _sut;

        private readonly GlobalSettings _globalSettings;

        public LocalSendStorageServiceTests()
        {
            _globalSettings = new GlobalSettings();

            _sut = new LocalSendStorageService(_globalSettings);
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
