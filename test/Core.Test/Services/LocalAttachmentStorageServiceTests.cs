using System;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class LocalAttachmentStorageServiceTests
    {
        private readonly LocalAttachmentStorageService _sut;

        private readonly GlobalSettings _globalSettings;

        public LocalAttachmentStorageServiceTests()
        {
            _globalSettings = new GlobalSettings();

            _sut = new LocalAttachmentStorageService(_globalSettings);
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
