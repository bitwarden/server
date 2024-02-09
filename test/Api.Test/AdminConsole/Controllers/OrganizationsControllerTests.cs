using System.Security.Claims;
using AutoFixture.Xunit2;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Queries;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationLicenses.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Services;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
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
    private readonly IPaymentService _paymentService;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ISsoConfigService _ssoConfigService;
    private readonly IUserService _userService;
    private readonly IGetOrganizationApiKeyQuery _getOrganizationApiKeyQuery;
    private readonly IRotateOrganizationApiKeyCommand _rotateOrganizationApiKeyCommand;
    private readonly IOrganizationApiKeyRepository _organizationApiKeyRepository;
    private readonly ICloudGetOrganizationLicenseQuery _cloudGetOrganizationLicenseQuery;
    private readonly ICreateOrganizationApiKeyCommand _createOrganizationApiKeyCommand;
    private readonly IFeatureService _featureService;
    private readonly ILicensingService _licensingService;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;
    private readonly IUpgradeOrganizationPlanCommand _upgradeOrganizationPlanCommand;
    private readonly IAddSecretsManagerSubscriptionCommand _addSecretsManagerSubscriptionCommand;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ICancelSubscriptionCommand _cancelSubscriptionCommand;
    private readonly IGetSubscriptionQuery _getSubscriptionQuery;
    private readonly IReferenceEventService _referenceEventService;
    private readonly IOrganizationEnableCollectionEnhancementsCommand _organizationEnableCollectionEnhancementsCommand;

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
        _getOrganizationApiKeyQuery = Substitute.For<IGetOrganizationApiKeyQuery>();
        _rotateOrganizationApiKeyCommand = Substitute.For<IRotateOrganizationApiKeyCommand>();
        _organizationApiKeyRepository = Substitute.For<IOrganizationApiKeyRepository>();
        _userService = Substitute.For<IUserService>();
        _cloudGetOrganizationLicenseQuery = Substitute.For<ICloudGetOrganizationLicenseQuery>();
        _createOrganizationApiKeyCommand = Substitute.For<ICreateOrganizationApiKeyCommand>();
        _featureService = Substitute.For<IFeatureService>();
        _licensingService = Substitute.For<ILicensingService>();
        _updateSecretsManagerSubscriptionCommand = Substitute.For<IUpdateSecretsManagerSubscriptionCommand>();
        _upgradeOrganizationPlanCommand = Substitute.For<IUpgradeOrganizationPlanCommand>();
        _addSecretsManagerSubscriptionCommand = Substitute.For<IAddSecretsManagerSubscriptionCommand>();
        _pushNotificationService = Substitute.For<IPushNotificationService>();
        _cancelSubscriptionCommand = Substitute.For<ICancelSubscriptionCommand>();
        _getSubscriptionQuery = Substitute.For<IGetSubscriptionQuery>();
        _referenceEventService = Substitute.For<IReferenceEventService>();
        _organizationEnableCollectionEnhancementsCommand = Substitute.For<IOrganizationEnableCollectionEnhancementsCommand>();

        _sut = new OrganizationsController(
            _organizationRepository,
            _organizationUserRepository,
            _policyRepository,
            _organizationService,
            _userService,
            _paymentService,
            _currentContext,
            _ssoConfigRepository,
            _ssoConfigService,
            _getOrganizationApiKeyQuery,
            _rotateOrganizationApiKeyCommand,
            _createOrganizationApiKeyCommand,
            _organizationApiKeyRepository,
            _cloudGetOrganizationLicenseQuery,
            _featureService,
            _globalSettings,
            _licensingService,
            _updateSecretsManagerSubscriptionCommand,
            _upgradeOrganizationPlanCommand,
            _addSecretsManagerSubscriptionCommand,
            _pushNotificationService,
            _cancelSubscriptionCommand,
            _getSubscriptionQuery,
            _referenceEventService,
            _organizationEnableCollectionEnhancementsCommand);
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
    public async Task OrganizationsController_PostUpgrade_UserCannotEditSubscription_ThrowsNotFoundException(
        Guid organizationId,
        OrganizationUpgradeRequestModel model)
    {
        _currentContext.EditSubscription(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PostUpgrade(organizationId.ToString(), model));
    }

    [Theory, AutoData]
    public async Task OrganizationsController_PostUpgrade_NonSMUpgrade_ReturnsCorrectResponse(
        Guid organizationId,
        OrganizationUpgradeRequestModel model,
        bool success,
        string paymentIntentClientSecret)
    {
        model.UseSecretsManager = false;

        _currentContext.EditSubscription(organizationId).Returns(true);

        _upgradeOrganizationPlanCommand.UpgradePlanAsync(organizationId, Arg.Any<OrganizationUpgrade>())
            .Returns(new Tuple<bool, string>(success, paymentIntentClientSecret));

        var response = await _sut.PostUpgrade(organizationId.ToString(), model);

        Assert.Equal(success, response.Success);
        Assert.Equal(paymentIntentClientSecret, response.PaymentIntentClientSecret);
    }

    [Theory, AutoData]
    public async Task OrganizationsController_PostUpgrade_SMUpgrade_ProvidesAccess_ReturnsCorrectResponse(
        Guid organizationId,
        Guid userId,
        OrganizationUpgradeRequestModel model,
        bool success,
        string paymentIntentClientSecret,
        OrganizationUser organizationUser)
    {
        model.UseSecretsManager = true;
        organizationUser.AccessSecretsManager = false;

        _currentContext.EditSubscription(organizationId).Returns(true);

        _upgradeOrganizationPlanCommand.UpgradePlanAsync(organizationId, Arg.Any<OrganizationUpgrade>())
            .Returns(new Tuple<bool, string>(success, paymentIntentClientSecret));

        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        _organizationUserRepository.GetByOrganizationAsync(organizationId, userId).Returns(organizationUser);

        var response = await _sut.PostUpgrade(organizationId.ToString(), model);

        Assert.Equal(success, response.Success);
        Assert.Equal(paymentIntentClientSecret, response.PaymentIntentClientSecret);

        await _organizationUserRepository.Received(1).ReplaceAsync(Arg.Is<OrganizationUser>(orgUser =>
            orgUser.Id == organizationUser.Id && orgUser.AccessSecretsManager == true));
    }

    [Theory, AutoData]
    public async Task OrganizationsController_PostUpgrade_SMUpgrade_NullOrgUser_ReturnsCorrectResponse(
        Guid organizationId,
        Guid userId,
        OrganizationUpgradeRequestModel model,
        bool success,
        string paymentIntentClientSecret)
    {
        model.UseSecretsManager = true;

        _currentContext.EditSubscription(organizationId).Returns(true);

        _upgradeOrganizationPlanCommand.UpgradePlanAsync(organizationId, Arg.Any<OrganizationUpgrade>())
            .Returns(new Tuple<bool, string>(success, paymentIntentClientSecret));

        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        _organizationUserRepository.GetByOrganizationAsync(organizationId, userId).ReturnsNull();

        var response = await _sut.PostUpgrade(organizationId.ToString(), model);

        Assert.Equal(success, response.Success);
        Assert.Equal(paymentIntentClientSecret, response.PaymentIntentClientSecret);

        await _organizationUserRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, AutoData]
    public async Task OrganizationsController_PostSubscribeSecretsManagerAsync_NullOrg_ThrowsNotFoundException(
        Guid organizationId,
        SecretsManagerSubscribeRequestModel model)
    {
        _organizationRepository.GetByIdAsync(organizationId).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PostSubscribeSecretsManagerAsync(organizationId, model));
    }

    [Theory, AutoData]
    public async Task OrganizationsController_PostSubscribeSecretsManagerAsync_UserCannotEditSubscription_ThrowsNotFoundException(
        Guid organizationId,
        SecretsManagerSubscribeRequestModel model,
        Organization organization)
    {
        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        _currentContext.EditSubscription(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PostSubscribeSecretsManagerAsync(organizationId, model));
    }

    [Theory, AutoData]
    public async Task OrganizationsController_PostSubscribeSecretsManagerAsync_ProvidesAccess_ReturnsCorrectResponse(
        Guid organizationId,
        SecretsManagerSubscribeRequestModel model,
        Organization organization,
        Guid userId,
        OrganizationUser organizationUser,
        OrganizationUserOrganizationDetails organizationUserOrganizationDetails)
    {
        organizationUser.AccessSecretsManager = false;

        var ssoConfigurationData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.KeyConnector,
            KeyConnectorUrl = "https://example.com"
        };

        organizationUserOrganizationDetails.Permissions = string.Empty;
        organizationUserOrganizationDetails.SsoConfig = ssoConfigurationData.Serialize();

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        _currentContext.EditSubscription(organizationId).Returns(true);

        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        _organizationUserRepository.GetByOrganizationAsync(organization.Id, userId).Returns(organizationUser);

        _organizationUserRepository.GetDetailsByUserAsync(userId, organization.Id, OrganizationUserStatusType.Confirmed)
            .Returns(organizationUserOrganizationDetails);

        var response = await _sut.PostSubscribeSecretsManagerAsync(organizationId, model);

        Assert.Equal(response.Id, organizationUserOrganizationDetails.OrganizationId);
        Assert.Equal(response.Name, organizationUserOrganizationDetails.Name);

        await _addSecretsManagerSubscriptionCommand.Received(1)
            .SignUpAsync(organization, model.AdditionalSmSeats, model.AdditionalServiceAccounts);
        await _organizationUserRepository.Received(1).ReplaceAsync(Arg.Is<OrganizationUser>(orgUser =>
            orgUser.Id == organizationUser.Id && orgUser.AccessSecretsManager == true));
    }

    [Theory, AutoData]
    public async Task OrganizationsController_PostSubscribeSecretsManagerAsync_NullOrgUser_ReturnsCorrectResponse(
        Guid organizationId,
        SecretsManagerSubscribeRequestModel model,
        Organization organization,
        Guid userId,
        OrganizationUserOrganizationDetails organizationUserOrganizationDetails)
    {
        var ssoConfigurationData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.KeyConnector,
            KeyConnectorUrl = "https://example.com"
        };

        organizationUserOrganizationDetails.Permissions = string.Empty;
        organizationUserOrganizationDetails.SsoConfig = ssoConfigurationData.Serialize();

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        _currentContext.EditSubscription(organizationId).Returns(true);

        _userService.GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        _organizationUserRepository.GetByOrganizationAsync(organization.Id, userId).ReturnsNull();

        _organizationUserRepository.GetDetailsByUserAsync(userId, organization.Id, OrganizationUserStatusType.Confirmed)
            .Returns(organizationUserOrganizationDetails);

        var response = await _sut.PostSubscribeSecretsManagerAsync(organizationId, model);

        Assert.Equal(response.Id, organizationUserOrganizationDetails.OrganizationId);
        Assert.Equal(response.Name, organizationUserOrganizationDetails.Name);

        await _addSecretsManagerSubscriptionCommand.Received(1)
            .SignUpAsync(organization, model.AdditionalSmSeats, model.AdditionalServiceAccounts);
        await _organizationUserRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(Arg.Any<OrganizationUser>());
    }

    [Theory, AutoData]
    public async Task EnableCollectionEnhancements_Success(Organization organization)
    {
        organization.FlexibleCollections = false;
        var admin = new OrganizationUser { UserId = Guid.NewGuid(), Type = OrganizationUserType.Admin };
        var owner = new OrganizationUser { UserId = Guid.NewGuid(), Type = OrganizationUserType.Owner };
        var user = new OrganizationUser { UserId = Guid.NewGuid(), Type = OrganizationUserType.User };
        var invited = new OrganizationUser
        {
            UserId = null,
            Type = OrganizationUserType.Admin,
            Email = "invited@example.com"
        };
        var orgUsers = new List<OrganizationUser> { admin, owner, user, invited };

        _currentContext.OrganizationOwner(organization.Id).Returns(true);
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        _organizationUserRepository.GetManyByOrganizationAsync(organization.Id, null).Returns(orgUsers);

        await _sut.EnableCollectionEnhancements(organization.Id);

        await _organizationEnableCollectionEnhancementsCommand.Received(1).EnableCollectionEnhancements(organization);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(admin.UserId.Value);
        await _pushNotificationService.Received(1).PushSyncVaultAsync(owner.UserId.Value);
        await _pushNotificationService.DidNotReceive().PushSyncVaultAsync(user.UserId.Value);
    }

    [Theory, AutoData]
    public async Task EnableCollectionEnhancements_WhenNotOwner_Throws(Organization organization)
    {
        organization.FlexibleCollections = false;
        _currentContext.OrganizationOwner(organization.Id).Returns(false);
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        await Assert.ThrowsAsync<NotFoundException>(async () => await _sut.EnableCollectionEnhancements(organization.Id));

        await _organizationEnableCollectionEnhancementsCommand.DidNotReceiveWithAnyArgs().EnableCollectionEnhancements(Arg.Any<Organization>());
        await _pushNotificationService.DidNotReceiveWithAnyArgs().PushSyncVaultAsync(Arg.Any<Guid>());
    }
}
