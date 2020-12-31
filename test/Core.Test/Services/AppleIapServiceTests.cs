using System;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class AppleIapServiceTests
    {
        private readonly AppleIapService _sut;

        private readonly GlobalSettings _globalSettings;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IMetaDataRepository _metaDateRepository;

        public AppleIapServiceTests()
        {
            _globalSettings = new GlobalSettings
            {
                AppleIap = new GlobalSettings.AppleIapSettings
                {
                    AppInReview = true,
                    Password = "AppleIapServicesTestsPassword",
                }
            };

            _webHostEnvironment = Substitute.For<IWebHostEnvironment>();
            _metaDateRepository = Substitute.For<IMetaDataRepository>();

            _sut = new AppleIapService(_globalSettings,
                _webHostEnvironment,
                _metaDateRepository,
                NullLogger<AppleIapService>.Instance);
        }

        [Fact]
        public void ServiceExists()
        {
            Assert.NotNull(_sut);
        }
    }
}
