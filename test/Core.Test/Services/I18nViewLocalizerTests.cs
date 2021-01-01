using System;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class I18nViewLocalizerTests
    {
        private readonly I18nViewLocalizer _sut;

        private readonly IStringLocalizerFactory _stringLocalizerFactory;
        private readonly IHtmlLocalizerFactory _htmlLocalizerFactory;

        public I18nViewLocalizerTests()
        {
            _stringLocalizerFactory = Substitute.For<IStringLocalizerFactory>();
            _htmlLocalizerFactory = Substitute.For<IHtmlLocalizerFactory>();

            _sut = new I18nViewLocalizer(_stringLocalizerFactory, _htmlLocalizerFactory);
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
