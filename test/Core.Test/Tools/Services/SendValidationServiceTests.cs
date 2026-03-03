using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Pricing.Premium;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

[SutProviderCustomize]
public class SendValidationServiceTests
{
    [Theory, BitAutoData]
    public async Task StorageRemainingForSendAsync_OrgGrantedPremiumUser_UsesPricingService(
        SutProvider<SendValidationService> sutProvider,
        Send send,
        User user)
    {
        // Arrange
        send.UserId = user.Id;
        send.OrganizationId = null;
        send.Type = SendType.File;
        user.Premium = false;
        user.Storage = 1024L * 1024L * 1024L; // 1 GB used
        user.EmailVerified = true;

        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        var premiumPlan = new Plan
        {
            Storage = new Purchasable { Provided = 5 }
        };
        sutProvider.GetDependency<IPricingClient>().GetAvailablePremiumPlan().Returns(premiumPlan);

        // Act
        var result = await sutProvider.Sut.StorageRemainingForSendAsync(send);

        // Assert
        await sutProvider.GetDependency<IPricingClient>().Received(1).GetAvailablePremiumPlan();
        Assert.True(result > 0);
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForSendAsync_IndividualPremium_DoesNotCallPricingService(
        SutProvider<SendValidationService> sutProvider,
        Send send,
        User user)
    {
        // Arrange
        send.UserId = user.Id;
        send.OrganizationId = null;
        send.Type = SendType.File;
        user.Premium = true;
        user.MaxStorageGb = 10;
        user.EmailVerified = true;

        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        // Act
        var result = await sutProvider.Sut.StorageRemainingForSendAsync(send);

        // Assert
        await sutProvider.GetDependency<IPricingClient>().DidNotReceive().GetAvailablePremiumPlan();
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForSendAsync_SelfHosted_DoesNotCallPricingService(
        SutProvider<SendValidationService> sutProvider,
        Send send,
        User user)
    {
        // Arrange
        send.UserId = user.Id;
        send.OrganizationId = null;
        send.Type = SendType.File;
        user.Premium = false;
        user.EmailVerified = true;

        sutProvider.GetDependency<Bit.Core.Settings.GlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(user.Id).Returns(user);
        sutProvider.GetDependency<IUserService>().CanAccessPremium(user).Returns(true);

        // Act
        var result = await sutProvider.Sut.StorageRemainingForSendAsync(send);

        // Assert
        await sutProvider.GetDependency<IPricingClient>().DidNotReceive().GetAvailablePremiumPlan();
    }

    [Theory, BitAutoData]
    public async Task StorageRemainingForSendAsync_OrgSend_DoesNotCallPricingService(
        SutProvider<SendValidationService> sutProvider,
        Send send,
        Organization org)
    {
        // Arrange
        send.UserId = null;
        send.OrganizationId = org.Id;
        send.Type = SendType.File;
        org.MaxStorageGb = 100;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);

        // Act
        var result = await sutProvider.Sut.StorageRemainingForSendAsync(send);

        // Assert
        await sutProvider.GetDependency<IPricingClient>().DidNotReceive().GetAvailablePremiumPlan();
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_ThrowsWhenDisableSendPolicyApplies(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = false;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().Organizations
            .Returns([new CurrentContextOrganization()]);
        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend)
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_ThrowsWhenSendOptionsDisableHideEmailApplies(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = true;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().Organizations
            .Returns([new CurrentContextOrganization()]);
        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend)
            .Returns(false);
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SendOptions)
            .Returns([new OrganizationUserPolicyDetails
            {
                PolicyData = CoreHelpers.ClassToJsonData(new SendOptionsPolicyData { DisableHideEmail = true })
            }]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_NoThrowWhenNoOrganizations(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().Organizations.Returns([]);

        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);

        await sutProvider.GetDependency<IPolicyService>()
            .DidNotReceive()
            .AnyPoliciesApplicableToUserAsync(Arg.Any<Guid>(), Arg.Any<PolicyType>());
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_SendControls_ThrowsWhenDisableSendPolicyApplies(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = false;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().Organizations
            .Returns([new CurrentContextOrganization()]);
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SendControls)
            .Returns([new OrganizationUserPolicyDetails
            {
                PolicyData = CoreHelpers.ClassToJsonData(new SendControlsPolicyData { DisableSend = true })
            }]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_SendControls_ThrowsWhenDisableHideEmailApplies(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = true;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().Organizations
            .Returns([new CurrentContextOrganization()]);
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SendControls)
            .Returns([new OrganizationUserPolicyDetails
            {
                PolicyData = CoreHelpers.ClassToJsonData(new SendControlsPolicyData { DisableHideEmail = true })
            }]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_SendControls_NoThrowWhenNoPoliciesApply(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = true;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(false);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().Organizations
            .Returns([new CurrentContextOrganization()]);
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SendControls)
            .Returns([]);

        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_VNext_ThrowsWhenSendControlsDisableSendApplies(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = false;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendControlsPolicyRequirement>(userId)
            .Returns(new SendControlsPolicyRequirement { DisableSend = true, DisableHideEmail = false });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_VNext_ThrowsWhenSendControlsDisableHideEmailApplies(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = true;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendControlsPolicyRequirement>(userId)
            .Returns(new SendControlsPolicyRequirement { DisableSend = false, DisableHideEmail = true });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_VNext_UsesLegacyDisableSendRequirement_WhenSendControlsFlagOff(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = false;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(false);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<DisableSendPolicyRequirement>(userId)
            .Returns(new DisableSendPolicyRequirement { DisableSend = true });

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_VNext_UsesPolicyRequirementQuery_WhenPolicyRequirementsFlagOn(
        SutProvider<SendValidationService> sutProvider,
        Guid userId,
        Send send)
    {
        send.HideEmail = false;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PolicyRequirements).Returns(true);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.SendControls).Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendControlsPolicyRequirement>(userId)
            .Returns(new SendControlsPolicyRequirement { DisableSend = false, DisableHideEmail = false });

        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);

        // Must use policyRequirementQuery, not IPolicyService
        await sutProvider.GetDependency<IPolicyRequirementQuery>()
            .Received(1)
            .GetAsync<SendControlsPolicyRequirement>(userId);
        await sutProvider.GetDependency<IPolicyService>()
            .DidNotReceive()
            .GetPoliciesApplicableToUserAsync(Arg.Any<Guid>(), Arg.Any<PolicyType>());
    }
}
