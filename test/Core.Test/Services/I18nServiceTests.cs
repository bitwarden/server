using System;
using Bit.Core.Services;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class I18nServiceTests
    {
        private readonly I18nService _sut;

        private readonly IStringLocalizerFactory _factory;

        public I18nServiceTests()
        {
            _factory = Substitute.For<IStringLocalizerFactory>();

            _sut = new I18nService(_factory);
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
