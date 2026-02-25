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

        // Assert - should NOT call pricing service for individual premium users
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

        // Assert - should NOT call pricing service for self-hosted
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

        // Assert - should NOT call pricing service for org sends
        await sutProvider.GetDependency<IPricingClient>().DidNotReceive().GetAvailablePremiumPlan();
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_vNext_DisableNoAuthSends_ThrowsWhenAuthTypeIsNull(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = null;
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendPolicyRequirement>(userId)
            .Returns(new SendPolicyRequirement { DisableNoAuthSends = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_vNext_DisableNoAuthSends_ThrowsWhenAuthTypeIsNone(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.None;
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendPolicyRequirement>(userId)
            .Returns(new SendPolicyRequirement { DisableNoAuthSends = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_vNext_DisableNoAuthSends_AllowsOtherAuthTypes(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.Password;
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendPolicyRequirement>(userId)
            .Returns(new SendPolicyRequirement { DisableNoAuthSends = true });

        // Should not throw
        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_vNext_DisablePasswordSends_ThrowsWhenPasswordAuth(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.Password;
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendPolicyRequirement>(userId)
            .Returns(new SendPolicyRequirement { DisablePasswordSends = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_vNext_DisablePasswordSends_AllowsOtherAuthTypes(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.Email;
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendPolicyRequirement>(userId)
            .Returns(new SendPolicyRequirement { DisablePasswordSends = true });

        // Should not throw
        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_vNext_DisableEmailVerifiedSends_ThrowsWhenEmailAuth(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.Email;
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendPolicyRequirement>(userId)
            .Returns(new SendPolicyRequirement { DisableEmailVerifiedSends = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_vNext_DisableEmailVerifiedSends_AllowsOtherAuthTypes(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.None;
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SendPolicyRequirement>(userId)
            .Returns(new SendPolicyRequirement { DisableEmailVerifiedSends = true });

        // Should not throw
        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }

    private static OrganizationUserPolicyDetails SendOptionsPolicyWith(SendPolicyData data) =>
        new()
        {
            PolicyType = PolicyType.SendOptions,
            PolicyEnabled = true,
            PolicyData = CoreHelpers.ClassToJsonData(data),
        };

    private static void SetupLegacyPath(
        Guid userId,
        SutProvider<SendValidationService> sutProvider,
        SendPolicyData policyData)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>().Organizations =
            [new CurrentContextOrganization()];
        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend)
            .Returns(false);
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.SendOptions)
            .Returns([SendOptionsPolicyWith(policyData)]);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_DisableSendOnSendOptions_Throws(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        SetupLegacyPath(userId, sutProvider, new SendPolicyData { DisableSend = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_DisableNoAuthSends_ThrowsWhenAuthTypeIsNull(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = null;
        SetupLegacyPath(userId, sutProvider, new SendPolicyData { DisableNoAuthSends = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_DisableNoAuthSends_ThrowsWhenAuthTypeIsNone(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.None;
        SetupLegacyPath(userId, sutProvider, new SendPolicyData { DisableNoAuthSends = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_DisableNoAuthSends_AllowsOtherAuthTypes(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.Password;
        SetupLegacyPath(userId, sutProvider, new SendPolicyData { DisableNoAuthSends = true });

        // Should not throw
        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_DisablePasswordSends_ThrowsWhenPasswordAuth(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.Password;
        SetupLegacyPath(userId, sutProvider, new SendPolicyData { DisablePasswordSends = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_DisablePasswordSends_AllowsOtherAuthTypes(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.Email;
        SetupLegacyPath(userId, sutProvider, new SendPolicyData { DisablePasswordSends = true });

        // Should not throw
        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_DisableEmailVerifiedSends_ThrowsWhenEmailAuth(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.Email;
        SetupLegacyPath(userId, sutProvider, new SendPolicyData { DisableEmailVerifiedSends = true });

        await Assert.ThrowsAsync<Exceptions.BadRequestException>(
            () => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_Legacy_DisableEmailVerifiedSends_AllowsOtherAuthTypes(
        Guid userId,
        Send send,
        SutProvider<SendValidationService> sutProvider)
    {
        send.AuthType = AuthType.None;
        SetupLegacyPath(userId, sutProvider, new SendPolicyData { DisableEmailVerifiedSends = true });

        // Should not throw
        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }
}
