using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Pricing.Premium;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Services;
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
    public async Task ValidateUserCanSaveAsync_WhenDisableSendPolicyEnforced_CannotCreateSend(
        SutProvider<SendValidationService> sutProvider, Send send, Guid userId)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<DisableSendPolicyRequirement>(userId)
            .Returns(new DisableSendPolicyRequirement { DisableSend = true });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
        Assert.Contains("you are only able to delete an existing Send", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_WhenSendOptionsPolicyProhibitsHidingEmail_CannotHideEmail(
        SutProvider<SendValidationService> sutProvider, Send send, Guid userId)
    {
        send.HideEmail = true;

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<DisableSendPolicyRequirement>(userId)
            .Returns(new DisableSendPolicyRequirement { DisableSend = false });

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendOptionsPolicyRequirement>(userId)
            .Returns(new SendOptionsPolicyRequirement { DisableHideEmail = true });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
        Assert.Contains("you are not allowed to hide your email address", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_WhenSendOptionsPolicyProhibitsHidingEmail_CanShowEmail(
        SutProvider<SendValidationService> sutProvider, Send send, Guid userId)
    {
        send.HideEmail = false;

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<DisableSendPolicyRequirement>(userId)
            .Returns(new DisableSendPolicyRequirement { DisableSend = false });

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendOptionsPolicyRequirement>(userId)
            .Returns(new SendOptionsPolicyRequirement { DisableHideEmail = true });
        
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendControlsPolicyRequirement>(userId)
            .Returns(new SendControlsPolicyRequirement { WhoCanAccess = SendWhoCanAccessType.Any });

        // No exception implies success
        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_WhenPoliciesDoNotApply_Success(
        SutProvider<SendValidationService> sutProvider, Send send, Guid userId)
    {
        send.HideEmail = true;

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<DisableSendPolicyRequirement>(userId)
            .Returns(new DisableSendPolicyRequirement { DisableSend = false });

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendOptionsPolicyRequirement>(userId)
            .Returns(new SendOptionsPolicyRequirement { DisableHideEmail = false });
        
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendControlsPolicyRequirement>(userId)
            .Returns(new SendControlsPolicyRequirement { WhoCanAccess = SendWhoCanAccessType.Any });

        // No exception implies success
        await sutProvider.Sut.ValidateUserCanSaveAsync(userId, send);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_WhenPasswordAuthRequiredByPolicy(
        SutProvider<SendValidationService> sutProvider, Send send, Guid userId
    )
    {
        send.AuthType = AuthType.None;
        send.Password = null;
        send.Emails = null;

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<DisableSendPolicyRequirement>(userId)
            .Returns(new DisableSendPolicyRequirement { DisableSend = false });

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendOptionsPolicyRequirement>(userId)
            .Returns(new SendOptionsPolicyRequirement { DisableHideEmail = false });
        
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendControlsPolicyRequirement>(userId)
            .Returns(new SendControlsPolicyRequirement { DisableSend = false, DisableHideEmail = false, WhoCanAccess = SendWhoCanAccessType.PasswordProtected });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
        Assert.Equal("Due to an Enterprise Policy your Sends must be protected by password", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_WhenEmailAuthRequiredByPolicy(
        SutProvider<SendValidationService> sutProvider, Send send, Guid userId
    )
    {
        send.AuthType = AuthType.Password;
        send.Password = "testpassword";
        send.Emails = null;

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<DisableSendPolicyRequirement>(userId)
            .Returns(new DisableSendPolicyRequirement { DisableSend = false });

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendOptionsPolicyRequirement>(userId)
            .Returns(new SendOptionsPolicyRequirement { DisableHideEmail = false });
        
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendControlsPolicyRequirement>(userId)
            .Returns(new SendControlsPolicyRequirement { DisableSend = false, DisableHideEmail = false, WhoCanAccess = SendWhoCanAccessType.SpecificPeople });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
        Assert.Equal("Due to an Enterprise Policy your Sends must be protected by email verification", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateUserCanSaveAsync_WhenEmailAuthAndDomainsRequiredByPolicy(
        SutProvider<SendValidationService> sutProvider, Send send, Guid userId
    )
    {
        send.AuthType = AuthType.Email;
        send.Password = null;
        send.Emails = "badguy@fake-bitwarden.com";

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<DisableSendPolicyRequirement>(userId)
            .Returns(new DisableSendPolicyRequirement { DisableSend = false });

        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendOptionsPolicyRequirement>(userId)
            .Returns(new SendOptionsPolicyRequirement { DisableHideEmail = false });
        
        sutProvider.GetDependency<IPolicyRequirementQuery>().GetAsync<SendControlsPolicyRequirement>(userId)
            .Returns(new SendControlsPolicyRequirement { DisableSend = false, DisableHideEmail = false, WhoCanAccess = SendWhoCanAccessType.SpecificPeople, AllowedDomains = "bitwarden.com" });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ValidateUserCanSaveAsync(userId, send));
        Assert.Equal("Due to an Enterprise Policy your Sends must be protected by email verification and access granted only to the following domain(s): bitwarden.com", exception.Message);
    }
}
