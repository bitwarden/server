using System.Security.Claims;
using AutoFixture.Xunit3;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Models.Request.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
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
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationsController))]
[SutProviderCustomize]
public class OrganizationsControllerTests
{
    [Theory, BitAutoData]
    public async Task OrganizationsController_UserCannotLeaveOrganizationThatProvidesKeyConnector(
        SutProvider<OrganizationsController> sutProvider,
        Guid orgId,
        User user)
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

        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(orgId).Returns(true);
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IUserService>().GetOrganizationsClaimingUserAsync(user.Id).Returns(new List<Organization> { null });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Leave(orgId));

        Assert.Contains("Your organization's Single Sign-On settings prevent you from leaving.",
            exception.Message);

        await sutProvider.GetDependency<IRemoveOrganizationUserCommand>().DidNotReceiveWithAnyArgs().UserLeaveAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task OrganizationsController_UserCannotLeaveOrganizationThatManagesUser(
        SutProvider<OrganizationsController> sutProvider,
        Guid orgId,
        User user)
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
        var foundOrg = new Organization
        {
            Id = orgId
        };

        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(orgId).Returns(true);
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IUserService>().GetOrganizationsClaimingUserAsync(user.Id).Returns(new List<Organization> { foundOrg });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Leave(orgId));

        Assert.Contains("Claimed user account cannot leave claiming organization. Contact your organization administrator for additional details.",
            exception.Message);

        await sutProvider.GetDependency<IRemoveOrganizationUserCommand>().DidNotReceiveWithAnyArgs().RemoveUserAsync(default, default);
    }

    [Theory]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    public async Task OrganizationsController_UserCanLeaveOrganizationThatDoesntProvideKeyConnector(
        bool keyConnectorEnabled,
        bool userUsesKeyConnector,
        SutProvider<OrganizationsController> sutProvider,
        Guid orgId,
        User user)
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

        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(orgId).Returns(true);
        sutProvider.GetDependency<ISsoConfigRepository>().GetByOrganizationIdAsync(orgId).Returns(ssoConfig);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IUserService>().GetOrganizationsClaimingUserAsync(user.Id).Returns(new List<Organization>());

        await sutProvider.Sut.Leave(orgId);

        await sutProvider.GetDependency<IRemoveOrganizationUserCommand>().Received(1).UserLeaveAsync(orgId, user.Id);
    }

    [Theory, BitAutoData]
    public async Task Delete_OrganizationIsConsolidatedBillingClient_ScalesProvidersSeats(
        SutProvider<OrganizationsController> sutProvider,
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

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId).Returns(organization);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IUserService>().VerifySecretAsync(user, requestModel.Secret).Returns(true);
        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organization.Id).Returns(provider);

        await sutProvider.Sut.Delete(organizationId.ToString(), requestModel);

        await sutProvider.GetDependency<IProviderBillingService>().Received(1)
            .ScaleSeats(provider, organization.PlanType, -organization.Seats.Value);

        await sutProvider.GetDependency<IOrganizationDeleteCommand>().Received(1).DeleteAsync(organization);
    }

    [Theory, BitAutoData]
    public async Task GetAutoEnrollStatus_WithPolicyRequirementsEnabled_ReturnsOrganizationAutoEnrollStatus_WithResetPasswordEnabledTrue(
        SutProvider<OrganizationsController> sutProvider,
        User user,
        Organization organization,
        OrganizationUser organizationUser)
    {
        var policyRequirement = new ResetPasswordPolicyRequirement { AutoEnrollOrganizations = [organization.Id] };

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(organization.Id.ToString()).Returns(organization);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(organization.Id, user.Id).Returns(organizationUser);
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<ResetPasswordPolicyRequirement>(user.Id).Returns(policyRequirement);

        var result = await sutProvider.Sut.GetAutoEnrollStatus(organization.Id.ToString());

        await sutProvider.GetDependency<IUserService>().Received(1).GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>());
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdentifierAsync(organization.Id.ToString());
        await sutProvider.GetDependency<IPolicyRequirementQuery>().Received(1).GetAsync<ResetPasswordPolicyRequirement>(user.Id);

        Assert.True(result.ResetPasswordEnabled);
        Assert.Equal(result.Id, organization.Id);
    }

    [Theory, BitAutoData]
    public async Task GetAutoEnrollStatus_WithPolicyRequirementsDisabled_ReturnsOrganizationAutoEnrollStatus_WithResetPasswordEnabledTrue(
        SutProvider<OrganizationsController> sutProvider,
        User user,
        Organization organization,
        OrganizationUser organizationUser)
    {
        var policy = new Policy
        {
            Type = PolicyType.ResetPassword,
            Enabled = true,
            Data = "{\"AutoEnrollEnabled\": true}",
            OrganizationId = organization.Id
        };

        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(organization.Id.ToString()).Returns(organization);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(organization.Id, user.Id).Returns(organizationUser);
        sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword).Returns(policy);

        var result = await sutProvider.Sut.GetAutoEnrollStatus(organization.Id.ToString());

        await sutProvider.GetDependency<IUserService>().Received(1).GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>());
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).GetByIdentifierAsync(organization.Id.ToString());
        await sutProvider.GetDependency<IPolicyRequirementQuery>().Received(0).GetAsync<ResetPasswordPolicyRequirement>(user.Id);
        await sutProvider.GetDependency<IPolicyRepository>().Received(1).GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword);

        Assert.True(result.ResetPasswordEnabled);
    }

    [Theory, BitAutoData]
    public async Task PutCollectionManagement_ValidRequest_Success(
        SutProvider<OrganizationsController> sutProvider,
        Organization organization,
        OrganizationCollectionManagementUpdateRequestModel model)
    {
        // Arrange
        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(true);

        var plan = MockPlans.Get(PlanType.EnterpriseAnnually);
        sutProvider.GetDependency<IPricingClient>().GetPlan(Arg.Any<PlanType>()).Returns(plan);

        sutProvider.GetDependency<IOrganizationService>()
            .UpdateCollectionManagementSettingsAsync(
                organization.Id,
                Arg.Is<OrganizationCollectionManagementSettings>(s =>
                    s.LimitCollectionCreation == model.LimitCollectionCreation &&
                    s.LimitCollectionDeletion == model.LimitCollectionDeletion &&
                    s.LimitItemDeletion == model.LimitItemDeletion &&
                    s.AllowAdminAccessToAllCollectionItems == model.AllowAdminAccessToAllCollectionItems))
            .Returns(organization);

        // Act
        await sutProvider.Sut.PutCollectionManagement(organization.Id, model);

        // Assert
        await sutProvider.GetDependency<IOrganizationService>()
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
