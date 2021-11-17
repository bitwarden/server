using AutoFixture.Xunit2;
using Bit.Api.Controllers;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using System.Threading.Tasks;
using System.Security.Claims;
using System;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Api.Test.Controllers
{
    public class OrganizationsControllerTests: IDisposable
    {
        private readonly GlobalSettings _globalSettings;
        private readonly ICurrentContext _currentContext;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IPaymentService _paymentService;
        private readonly IPolicyRepository _policyRepository;
        private readonly ISsoConfigRepository _ssoConfigRepository;
        private readonly ISsoConfigService _ssoConfigService;
        private readonly IUserService _userService;

        private readonly OrganizationsController _sut;

        public OrganizationsControllerTests()
        {
            _currentContext = Substitute.For<ICurrentContext>();
            _globalSettings = Substitute.For<GlobalSettings>();
            _organizationRepository = Substitute.For<IOrganizationRepository>();
            _organizationService = Substitute.For<IOrganizationService>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _paymentService = Substitute.For<IPaymentService>();
            _policyRepository = Substitute.For<IPolicyRepository>();
            _ssoConfigRepository = Substitute.For<ISsoConfigRepository>();
            _ssoConfigService = Substitute.For<ISsoConfigService>();
            _userService = Substitute.For<IUserService>();

            _sut = new OrganizationsController(_organizationRepository, _organizationUserRepository,
                _policyRepository, _organizationService, _userService, _paymentService, _currentContext,
                _ssoConfigRepository, _ssoConfigService, _globalSettings);
        }

        public void Dispose()
        {
            _sut?.Dispose();
        }

        [Theory, AutoData]
        public async Task OrganizationsController_WhenUserTriestoLeaveOrganizationUsingKeyConnector_Throws(
            Guid orgId)
        {
            var ssoConfig = new SsoConfig
            {
                Id = default,
                Data = new SsoConfigurationData
                {
                    KeyConnectorEnabled = true,
                }.Serialize(),
                Enabled = true,
                OrganizationId = orgId,
            };

            _currentContext.OrganizationUser(orgId).Returns(true);
            _ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
            _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(new Guid());

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => _sut.Leave(orgId.ToString()));

            Assert.Contains("You cannot leave an Organization that is using Key Connector.",
                exception.Message);

            await _organizationService.DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
        }
    }
}

