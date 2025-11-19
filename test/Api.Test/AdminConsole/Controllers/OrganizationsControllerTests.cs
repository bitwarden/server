using System.Security.Claims;
using AutoFixture.Xunit2;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Models.Request.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.Models.Business.Tokenables;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks;
using Bit.Core.Tokens;
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
    private readonly IProviderRepository _providerRepository;
    private readonly IProviderBillingService _providerBillingService;
    private readonly IDataProtectorTokenFactory<OrgDeleteTokenable> _orgDeleteTokenDataFactory;
    private readonly IRemoveOrganizationUserCommand _removeOrganizationUserCommand;
    private readonly ICloudOrganizationSignUpCommand _cloudOrganizationSignUpCommand;
    private readonly IOrganizationDeleteCommand _organizationDeleteCommand;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly IPricingClient _pricingClient;
    private readonly IOrganizationUpdateKeysCommand _organizationUpdateKeysCommand;
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
        _providerRepository = Substitute.For<IProviderRepository>();
        _providerBillingService = Substitute.For<IProviderBillingService>();
        _orgDeleteTokenDataFactory = Substitute.For<IDataProtectorTokenFactory<OrgDeleteTokenable>>();
        _removeOrganizationUserCommand = Substitute.For<IRemoveOrganizationUserCommand>();
        _cloudOrganizationSignUpCommand = Substitute.For<ICloudOrganizationSignUpCommand>();
        _organizationDeleteCommand = Substitute.For<IOrganizationDeleteCommand>();
        _policyRequirementQuery = Substitute.For<IPolicyRequirementQuery>();
        _pricingClient = Substitute.For<IPricingClient>();
        _organizationUpdateKeysCommand = Substitute.For<IOrganizationUpdateKeysCommand>();

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
            _providerRepository,
            _providerBillingService,
            _orgDeleteTokenDataFactory,
            _removeOrganizationUserCommand,
            _cloudOrganizationSignUpCommand,
            _organizationDeleteCommand,
            _policyRequirementQuery,
            _pricingClient,
            _organizationUpdateKeysCommand);
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
        _userService.GetOrganizationsClaimingUserAsync(user.Id).Returns(new List<Organization> { null });
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Leave(orgId));

        Assert.Contains("Your organization's Single Sign-On settings prevent you from leaving.",
            exception.Message);

        await _removeOrganizationUserCommand.DidNotReceiveWithAnyArgs().UserLeaveAsync(default, default);
    }

    [Theory, AutoData]
    public async Task OrganizationsController_UserCannotLeaveOrganizationThatManagesUser(
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
        var foundOrg = new Organization();
        foundOrg.Id = orgId;

        _currentContext.OrganizationUser(orgId).Returns(true);
        _ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _userService.GetOrganizationsClaimingUserAsync(user.Id).Returns(new List<Organization> { { foundOrg } });
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.Leave(orgId));

        Assert.Contains("Claimed user account cannot leave claiming organization. Contact your organization administrator for additional details.",
            exception.Message);

        await _removeOrganizationUserCommand.DidNotReceiveWithAnyArgs().RemoveUserAsync(default, default);
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
        _userService.GetOrganizationsClaimingUserAsync(user.Id).Returns(new List<Organization>());

        await _sut.Leave(orgId);

        await _removeOrganizationUserCommand.Received(1).UserLeaveAsync(orgId, user.Id);
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

        _providerRepository.GetByOrganizationIdAsync(organization.Id).Returns(provider);

        await _sut.Delete(organizationId.ToString(), requestModel);

        await _providerBillingService.Received(1)
            .ScaleSeats(provider, organization.PlanType, -organization.Seats.Value);

        await _organizationDeleteCommand.Received(1).DeleteAsync(organization);
    }

    [Theory, AutoData]
    public async Task GetAutoEnrollStatus_WithPolicyRequirementsEnabled_ReturnsOrganizationAutoEnrollStatus_WithResetPasswordEnabledTrue(
        User user,
        Organization organization,
        OrganizationUser organizationUser
    )
    {
        var policyRequirement = new ResetPasswordPolicyRequirement() { AutoEnrollOrganizations = [organization.Id] };

        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _organizationRepository.GetByIdentifierAsync(organization.Id.ToString()).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id).Returns(organizationUser);
        _policyRequirementQuery.GetAsync<ResetPasswordPolicyRequirement>(user.Id).Returns(policyRequirement);

        var result = await _sut.GetAutoEnrollStatus(organization.Id.ToString());

        await _userService.Received(1).GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>());
        await _organizationRepository.Received(1).GetByIdentifierAsync(organization.Id.ToString());
        await _policyRequirementQuery.Received(1).GetAsync<ResetPasswordPolicyRequirement>(user.Id);

        Assert.True(result.ResetPasswordEnabled);
        Assert.Equal(result.Id, organization.Id);
    }

    [Theory, AutoData]
    public async Task GetAutoEnrollStatus_WithPolicyRequirementsDisabled_ReturnsOrganizationAutoEnrollStatus_WithResetPasswordEnabledTrue(
    User user,
    Organization organization,
    OrganizationUser organizationUser
)
    {

        var policy = new Policy() { Type = PolicyType.ResetPassword, Enabled = true, Data = "{\"AutoEnrollEnabled\": true}", OrganizationId = organization.Id };

        _userService.GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        _organizationRepository.GetByIdentifierAsync(organization.Id.ToString()).Returns(organization);
        _featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);
        _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id).Returns(organizationUser);
        _policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword).Returns(policy);

        var result = await _sut.GetAutoEnrollStatus(organization.Id.ToString());

        await _userService.Received(1).GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>());
        await _organizationRepository.Received(1).GetByIdentifierAsync(organization.Id.ToString());
        await _policyRequirementQuery.Received(0).GetAsync<ResetPasswordPolicyRequirement>(user.Id);
        await _policyRepository.Received(1).GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);

        Assert.True(result.ResetPasswordEnabled);
    }

    [Theory, AutoData]
    public async Task PutCollectionManagement_ValidRequest_Success(
        Organization organization,
        OrganizationCollectionManagementUpdateRequestModel model)
    {
        // Arrange
        _currentContext.OrganizationOwner(organization.Id).Returns(true);

        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlan(Arg.Any<PlanType>()).Returns(plan);

        _organizationService
            .UpdateCollectionManagementSettingsAsync(
                organization.Id,
                Arg.Is<OrganizationCollectionManagementSettings>(s =>
                    s.LimitCollectionCreation == model.LimitCollectionCreation &&
                    s.LimitCollectionDeletion == model.LimitCollectionDeletion &&
                    s.LimitItemDeletion == model.LimitItemDeletion &&
                    s.AllowAdminAccessToAllCollectionItems == model.AllowAdminAccessToAllCollectionItems))
            .Returns(organization);

        // Act
        await _sut.PutCollectionManagement(organization.Id, model);

        // Assert
        await _organizationService
            .Received(1)
            .UpdateCollectionManagementSettingsAsync(
                organization.Id,
                Arg.Is<OrganizationCollectionManagementSettings>(s =>
                    s.LimitCollectionCreation == model.LimitCollectionCreation &&
                    s.LimitCollectionDeletion == model.LimitCollectionDeletion &&
                    s.LimitItemDeletion == model.LimitItemDeletion &&
                    s.AllowAdminAccessToAllCollectionItems == model.AllowAdminAccessToAllCollectionItems));
    }
}
