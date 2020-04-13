using System;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class LicensingServiceTests
    {
        private readonly LicensingService _sut;

        private readonly GlobalSettings _globalSettings;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<LicensingService> _logger;

        public LicensingServiceTests()
        {
            _userRepository = Substitute.For<IUserRepository>();
            _organizationRepository = Substitute.For<IOrganizationRepository>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _hostingEnvironment = Substitute.For<IWebHostEnvironment>();
            _logger = Substitute.For<ILogger<LicensingService>>();
            _globalSettings = new GlobalSettings { SelfHosted = true, LicenseDirectory = "test" };

            _sut = new LicensingService(
                _userRepository,
                _organizationRepository,
                _organizationUserRepository,
                _hostingEnvironment,
                _logger,
                _globalSettings
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
