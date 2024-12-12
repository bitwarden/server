using System.Security.Claims;
using AutoFixture.Xunit2;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Billing.Controllers;
using Bit.Api.Models.Request.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Billing.Repositories;
using Bit.Core.Billing.Services;
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

namespace Bit.Api.Test.Billing.Controllers;

public class OrganizationsControllerTests : IDisposable
{
    private readonly GlobalSettings _globalSettings;
    private readonly ICurrentContext _currentContext;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPaymentService _paymentService;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IUserService _userService;
    private readonly ICloudGetOrganizationLicenseQuery _cloudGetOrganizationLicenseQuery;
    private readonly ILicensingService _licensingService;
    private readonly IUpdateSecretsManagerSubscriptionCommand _updateSecretsManagerSubscriptionCommand;
    private readonly IUpgradeOrganizationPlanCommand _upgradeOrganizationPlanCommand;
    private readonly IAddSecretsManagerSubscriptionCommand _addSecretsManagerSubscriptionCommand;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ISubscriberService _subscriberService;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;
    private readonly IOrganizationInstallationRepository _organizationInstallationRepository;

    private readonly OrganizationsController _sut;

    public OrganizationsControllerTests()
    {
        _currentContext = Substitute.For<ICurrentContext>();
        _globalSettings = Substitute.For<GlobalSettings>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _organizationService = Substitute.For<IOrganizationService>();
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _paymentService = Substitute.For<IPaymentService>();
        Substitute.For<IPolicyRepository>();
        _ssoConfigRepository = Substitute.For<ISsoConfigRepository>();
        Substitute.For<ISsoConfigService>();
        _userService = Substitute.For<IUserService>();
        _cloudGetOrganizationLicenseQuery = Substitute.For<ICloudGetOrganizationLicenseQuery>();
        _licensingService = Substitute.For<ILicensingService>();
        _updateSecretsManagerSubscriptionCommand = Substitute.For<IUpdateSecretsManagerSubscriptionCommand>();
        _upgradeOrganizationPlanCommand = Substitute.For<IUpgradeOrganizationPlanCommand>();
        _addSecretsManagerSubscriptionCommand = Substitute.For<IAddSecretsManagerSubscriptionCommand>();
        _referenceEventService = Substitute.For<IReferenceEventService>();
        _subscriberService = Substitute.For<ISubscriberService>();
        _removeOrganizationUserCommand = Substitute.For<IRemoveOrganizationUserCommand>();
        _organizationInstallationRepository = Substitute.For<IOrganizationInstallationRepository>();

        _sut = new OrganizationsController(
            _organizationRepository,
            _organizationUserRepository,
            _organizationService,
            _userService,
            _paymentService,
            _currentContext,
            _cloudGetOrganizationLicenseQuery,
            _globalSettings,
            _licensingService,
            _updateSecretsManagerSubscriptionCommand,
            _upgradeOrganizationPlanCommand,
            _addSecretsManagerSubscriptionCommand,
            _referenceEventService,
            _subscriberService,
            _organizationInstallationRepository);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Theory, AutoData]
    public async Task OrganizationsController_PostUpgrade_UserCannotEditSubscription_ThrowsNotFoundException(
        Guid organizationId,
        OrganizationUpgradeRequestModel model)
    {
        _currentContext.EditSubscription(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PostUpgrade(organizationId, model));
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

        var response = await _sut.PostUpgrade(organizationId, model);

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

        var response = await _sut.PostUpgrade(organizationId, model);

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

        var response = await _sut.PostUpgrade(organizationId, model);

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
}
