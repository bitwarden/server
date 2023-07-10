using System.Security.Claims;
using AutoFixture.Xunit2;
using Bit.Api.Controllers;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

public class OrganizationsControllerTests : IDisposable
{
    private readonly GlobalSettings _globalSettings;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly IPolicyRepository _policyRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ISsoConfigService _ssoConfigService;
    private readonly IUserService _userService;
    private readonly IGetOrganizationApiKeyQuery _getOrganizationApiKeyQuery;
    private readonly IRotateOrganizationApiKeyCommand _rotateOrganizationApiKeyCommand;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly ICloudGetOrganizationLicenseQuery _cloudGetOrganizationLicenseQuery;
    private readonly ICreateOrganizationApiKeyCommand _createOrganizationApiKeyCommand;
    private readonly IUpdateOrganizationLicenseCommand _updateOrganizationLicenseCommand;
    private readonly IFeatureService _featureService;
    private readonly ILicensingService _licensingService;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;

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
        _providerRepository = Substitute.For<IProviderRepository>();
        _ssoConfigRepository = Substitute.For<ISsoConfigRepository>();
        _ssoConfigService = Substitute.For<ISsoConfigService>();
        _getOrganizationApiKeyQuery = Substitute.For<IGetOrganizationApiKeyQuery>();
        _rotateOrganizationApiKeyCommand = Substitute.For<IRotateOrganizationApiKeyCommand>();
        _organizationApiKeyRepository = Substitute.For<IOrganizationApiKeyRepository>();
        _userService = Substitute.For<IUserService>();
        _cloudGetOrganizationLicenseQuery = Substitute.For<ICloudGetOrganizationLicenseQuery>();
        _createOrganizationApiKeyCommand = Substitute.For<ICreateOrganizationApiKeyCommand>();
        _updateOrganizationLicenseCommand = Substitute.For<IUpdateOrganizationLicenseCommand>();
        _featureService = Substitute.For<IFeatureService>();
        _licensingService = Substitute.For<ILicensingService>();
        _updateSecretsManagerSubscriptionCommand = Substitute.For<IUpdateSecretsManagerSubscriptionCommand>();

        _sut = new OrganizationsController(_organizationRepository, _organizationUserRepository,
            _policyRepository, _providerRepository, _organizationService, _userService, _paymentService, _currentContext,
            _ssoConfigRepository, _ssoConfigService, _getOrganizationApiKeyQuery, _rotateOrganizationApiKeyCommand,
            _createOrganizationApiKeyCommand, _organizationApiKeyRepository, _updateOrganizationLicenseCommand,
            _cloudGetOrganizationLicenseQuery, _featureService, _globalSettings, _licensingService,
            _updateSecretsManagerSubscriptionCommand);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Theory, AutoData]
    public async Task OrganizationsController_UserCannotLeaveOrganizationThatProvidesKeyConnector(
        Guid orgId, User user)
    {
        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.KeyConnector
            }.Serialize(),
            Enabled = true,
            OrganizationId = orgId,
        };

        user.UsesKeyConnector = true;

        _currentContext.OrganizationUser(orgId).Returns(true);
        _ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.Leave(orgId.ToString()));

        Assert.Contains("Your organization's Single Sign-On settings prevent you from leaving.",
            exception.Message);

        await _organizationService.DidNotReceiveWithAnyArgs().DeleteUserAsync(default, default);
    }

    [Theory]
    [InlineAutoData(true, false)]
    [InlineAutoData(false, true)]
    [InlineAutoData(false, false)]
    public async Task OrganizationsController_UserCanLeaveOrganizationThatDoesntProvideKeyConnector(
        bool keyConnectorEnabled, bool userUsesKeyConnector, Guid orgId, User user)
    {
        var ssoConfig = new SsoConfig
        {
            Id = default,
            Data = new SsoConfigurationData
            {
                MemberDecryptionType = keyConnectorEnabled
                    ? MemberDecryptionType.KeyConnector
                    : MemberDecryptionType.MasterPassword
            }.Serialize(),
            Enabled = true,
            OrganizationId = orgId,
        };

        user.UsesKeyConnector = userUsesKeyConnector;

        _currentContext.OrganizationUser(orgId).Returns(true);
        _ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);

        await _organizationService.DeleteUserAsync(orgId, user.Id);
        await _organizationService.Received(1).DeleteUserAsync(orgId, user.Id);
    }
}
