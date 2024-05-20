using System.Security.Claims;
using AutoFixture.Xunit2;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Billing.Commands;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Api.Test.AdminConsole.Controllers;

public class OrganizationsControllerTests : IDisposable
{
    private readonly GlobalSettings _globalSettings;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ISsoConfigService _ssoConfigService;
    private readonly IUserService _userService;
    private readonly IGetOrganizationApiKeyQuery _getOrganizationApiKeyQuery;
    private readonly IRotateOrganizationApiKeyCommand _rotateOrganizationApiKeyCommand;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly ICreateOrganizationApiKeyCommand _createOrganizationApiKeyCommand;
    private readonly IFeatureService _featureService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IOrganizationEnableCollectionEnhancementsCommand _organizationEnableCollectionEnhancementsCommand;
    private readonly IProviderRepository _providerRepository;
    private readonly IScaleSeatsCommand _scaleSeatsCommand;

    private readonly OrganizationsController _sut;

    public OrganizationsControllerTests()
    {
        _currentContext = Substitute.For<ICurrentContext>();
        _globalSettings = Substitute.For<GlobalSettings>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _organizationService = Substitute.For<IOrganizationService>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _policyRepository = Substitute.For<IPolicyRepository>();
        _ssoConfigRepository = Substitute.For<ISsoConfigRepository>();
        _ssoConfigService = Substitute.For<ISsoConfigService>();
        _getOrganizationApiKeyQuery = Substitute.For<IGetOrganizationApiKeyQuery>();
        _rotateOrganizationApiKeyCommand = Substitute.For<IRotateOrganizationApiKeyCommand>();
        _organizationApiKeyRepository = Substitute.For<IOrganizationApiKeyRepository>();
        _userService = Substitute.For<IUserService>();
        _createOrganizationApiKeyCommand = Substitute.For<ICreateOrganizationApiKeyCommand>();
        _featureService = Substitute.For<IFeatureService>();
        _pushNotificationService = Substitute.For<IPushNotificationService>();
        _organizationEnableCollectionEnhancementsCommand = Substitute.For<IOrganizationEnableCollectionEnhancementsCommand>();
        _providerRepository = Substitute.For<IProviderRepository>();
        _scaleSeatsCommand = Substitute.For<IScaleSeatsCommand>();

        _sut = new OrganizationsController(
            _organizationRepository,
            _organizationUserRepository,
            _policyRepository,
            _organizationService,
            _userService,
            _currentContext,
            _ssoConfigRepository,
            _ssoConfigService,
            _getOrganizationApiKeyQuery,
            _rotateOrganizationApiKeyCommand,
            _createOrganizationApiKeyCommand,
            _organizationApiKeyRepository,
            _featureService,
            _globalSettings,
            _pushNotificationService,
            _organizationEnableCollectionEnhancementsCommand,
            _providerRepository,
            _scaleSeatsCommand);
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

    [Theory, AutoData]
    public async Task EnableCollectionEnhancements_Success(Organization organization)
    {
        organization.FlexibleCollections = false;
        var admin = new OrganizationUser { UserId = Guid.NewGuid(), Type = OrganizationUserType.Admin, Status = OrganizationUserStatusType.Confirmed };
        var owner = new OrganizationUser { UserId = Guid.NewGuid(), Type = OrganizationUserType.Owner, Status = OrganizationUserStatusType.Confirmed };
        var user = new OrganizationUser { UserId = Guid.NewGuid(), Type = OrganizationUserType.User, Status = OrganizationUserStatusType.Confirmed };
        var invited = new OrganizationUser
        {
            UserId = null,
            Type = OrganizationUserType.Admin,
            Email = "invited@example.com",
            Status = OrganizationUserStatusType.Invited
        };
        var orgUsers = new List<OrganizationUser> { admin, owner, user, invited };

        _currentContext.OrganizationOwner(organization.Id).Returns(true);
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        _organizationUserRepository.GetManyByOrganizationAsync(organization.Id, null).Returns(orgUsers);

        await _sut.EnableCollectionEnhancements(organization.Id);

        await _organizationEnableCollectionEnhancementsCommand.Received(1).EnableCollectionEnhancements(organization);
        await _pushNotificationService.Received(1).PushSyncOrganizationsAsync(admin.UserId.Value);
        await _pushNotificationService.Received(1).PushSyncOrganizationsAsync(owner.UserId.Value);
        await _pushNotificationService.DidNotReceive().PushSyncOrganizationsAsync(user.UserId.Value);
        // Invited orgUser does not have a UserId we can use to assert here, but sut will throw if that null isn't handled
    }

    [Theory, AutoData]
    public async Task EnableCollectionEnhancements_WhenNotOwner_Throws(Organization organization)
    {
        organization.FlexibleCollections = false;
        _currentContext.OrganizationOwner(organization.Id).Returns(false);
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        await Assert.ThrowsAsync<NotFoundException>(async () => await _sut.EnableCollectionEnhancements(organization.Id));

        await _organizationEnableCollectionEnhancementsCommand.DidNotReceiveWithAnyArgs().EnableCollectionEnhancements(Arg.Any<Organization>());
        await _pushNotificationService.DidNotReceiveWithAnyArgs().PushSyncOrganizationsAsync(Arg.Any<Guid>());
    }

    [Theory, AutoData]
    public async Task Delete_OrganizationIsConsolidatedBillingClient_ScalesProvidersSeats(
        Provider provider,
        Organization organization,
        User user,
        Guid organizationId,
        SecretVerificationRequestModel requestModel)
    {
        organization.Status = OrganizationStatusType.Managed;
        organization.PlanType = PlanType.TeamsMonthly;
        organization.Seats = 10;

        provider.Type = ProviderType.Msp;
        provider.Status = ProviderStatusType.Billable;

        _currentContext.OrganizationOwner(organizationId).Returns(true);

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);

        _userService.VerifySecretAsync(user, requestModel.Secret).Returns(true);

        _featureService.IsEnabled(FeatureFlagKeys.EnableConsolidatedBilling).Returns(true);

        _providerRepository.GetByOrganizationIdAsync(organization.Id).Returns(provider);

        await _sut.Delete(organizationId.ToString(), requestModel);

        await _scaleSeatsCommand.Received(1)
            .ScalePasswordManagerSeats(provider, organization.PlanType, -organization.Seats.Value);

        await _organizationService.Received(1).DeleteAsync(organization);
    }
}
