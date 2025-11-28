using System.Text;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.UserFeatures.Registration.Implementations;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.Registration;

[SutProviderCustomize]
public class RegisterUserCommandTests
{
    // -----------------------------------------------------------------------------------------------
    // RegisterUser tests
    // -----------------------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RegisterUser_Succeeds(SutProvider<RegisterUserCommand> sutProvider, User user)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Success);

        // Act
        var result = await sutProvider.Sut.RegisterUser(user);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendWelcomeEmailAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUser_WhenCreateUserFails_ReturnsIdentityResultFailed(SutProvider<RegisterUserCommand> sutProvider, User user)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Failed());

        // Act
        var result = await sutProvider.Sut.RegisterUser(user);

        // Assert
        Assert.False(result.Succeeded);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(user);

        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendWelcomeEmailAsync(Arg.Any<User>());
    }

    // -----------------------------------------------------------------------------------------------
    // RegisterSSOAutoProvisionedUserAsync tests
    // -----------------------------------------------------------------------------------------------
    [Theory, BitAutoData]
    public async Task RegisterSSOAutoProvisionedUserAsync_Success(
        User user,
        Organization organization,
        SutProvider<RegisterUserCommand> sutProvider)
    {
        // Arrange
        user.Id = Guid.NewGuid();
        organization.Id = Guid.NewGuid();
        organization.Name = "Test Organization";

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.RegisterSSOAutoProvisionedUserAsync(user, organization);

        // Assert
        Assert.True(result.Succeeded);
        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(user);
    }

    [Theory, BitAutoData]
    public async Task RegisterSSOAutoProvisionedUserAsync_UserRegistrationFails_ReturnsFailedResult(
        User user,
        Organization organization,
        SutProvider<RegisterUserCommand> sutProvider)
    {
        // Arrange
        var expectedError = new IdentityError();
        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Failed(expectedError));

        // Act
        var result = await sutProvider.Sut.RegisterSSOAutoProvisionedUserAsync(user, organization);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(expectedError, result.Errors);
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendOrganizationUserWelcomeEmailAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData(PlanType.EnterpriseAnnually)]
    [BitAutoData(PlanType.EnterpriseMonthly)]
    [BitAutoData(PlanType.TeamsAnnually)]
    public async Task RegisterSSOAutoProvisionedUserAsync_EnterpriseOrg_SendsOrganizationWelcomeEmail(
        PlanType planType,
        User user,
        Organization organization,
        SutProvider<RegisterUserCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = planType;
        organization.Name = "Enterprise Org";

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((OrganizationUser)null);

        // Act
        await sutProvider.Sut.RegisterSSOAutoProvisionedUserAsync(user, organization);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserWelcomeEmailAsync(user, organization.Name);
    }

    [Theory, BitAutoData]
    public async Task RegisterSSOAutoProvisionedUserAsync_FeatureFlagDisabled_SendsLegacyWelcomeEmail(
        User user,
        Organization organization,
        SutProvider<RegisterUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(false);

        // Act
        await sutProvider.Sut.RegisterSSOAutoProvisionedUserAsync(user, organization);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendWelcomeEmailAsync(user);
    }

    // -----------------------------------------------------------------------------------------------
    // RegisterUserWithOrganizationInviteToken tests
    // -----------------------------------------------------------------------------------------------

    // Simple happy path test
    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaOrganizationInviteToken_NoOrgInviteOrOrgUserIdOrReferenceData_Succeeds(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash)
    {
        // Arrange
        user.ReferenceData = null;

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        // Act
        var result = await sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, null, null);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(user, masterPasswordHash);
    }

    // Complex happy path test
    [Theory]
    [BitAutoData(false, null)]
    [BitAutoData(true, "sampleInitiationPath")]
    [BitAutoData(true, "Secrets Manager trial")]
    public async Task RegisterUserViaOrganizationInviteToken_ComplexHappyPath_Succeeds(bool addUserReferenceData, string initiationPath,
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, OrganizationUser orgUser, string orgInviteToken, Guid orgUserId, Policy twoFactorPolicy)
    {
        // Arrange
        sutProvider.GetDependency<IGlobalSettings>()
            .DisableUserRegistration.Returns(false);

        sutProvider.GetDependency<IGlobalSettings>()
            .DisableUserRegistration.Returns(true);

        orgUser.Email = user.Email;
        orgUser.Id = orgUserId;

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser);

        sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>()
            .TryUnprotect(orgInviteToken, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = orgInviteTokenable;
                return true;
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUserId)
            .Returns(orgUser);

        twoFactorPolicy.Enabled = true;
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(orgUser.OrganizationId, PolicyType.TwoFactorAuthentication)
            .Returns(twoFactorPolicy);

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        user.ReferenceData = addUserReferenceData ? $"{{\"initiationPath\":\"{initiationPath}\"}}" : null;

        // Act
        var result = await sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, orgInviteToken, orgUserId);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetByIdAsync(orgUserId);

        await sutProvider.GetDependency<IPolicyRepository>()
            .Received(1)
            .GetByOrganizationIdTypeAsync(orgUser.OrganizationId, PolicyType.TwoFactorAuthentication);

        sutProvider.GetDependency<IUserService>()
            .Received(1)
            .SetTwoFactorProvider(user, TwoFactorProviderType.Email);

        // example serialized data: {"1":{"Enabled":true,"MetaData":{"Email":"0dbf746c-deaf-4318-811e-d98ea7155075"}}}
        var twoFactorProviders = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new TwoFactorProvider
            {
                MetaData = new Dictionary<string, object> { ["Email"] = user.Email.ToLowerInvariant() },
                Enabled = true
            }
        };

        var serializedTwoFactorProviders =
            JsonHelpers.LegacySerialize(twoFactorProviders, JsonHelpers.LegacyEnumKeyResolver);

        Assert.Equal(user.TwoFactorProviders, serializedTwoFactorProviders);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(Arg.Is<User>(u => u.EmailVerified == true && u.ApiKey != null), masterPasswordHash);

        if (addUserReferenceData)
        {
            if (initiationPath.Contains("Secrets Manager trial"))
            {
                await sutProvider.GetDependency<IMailService>()
                    .Received(1)
                    .SendTrialInitiationEmailAsync(user.Email);
            }
            else
            {
                await sutProvider.GetDependency<IMailService>()
                    .Received(1)
                    .SendWelcomeEmailAsync(user);
            }
        }
        else
        {
            // Even if user doesn't have reference data, we should send them welcome email
            await sutProvider.GetDependency<IMailService>()
                .Received(1)
                .SendWelcomeEmailAsync(user);
        }

        Assert.True(result.Succeeded);

    }

    [Theory]
    [BitAutoData("invalidOrgInviteToken")]
    [BitAutoData("nullOrgInviteToken")]
    [BitAutoData("nullOrgUserId")]
    public async Task RegisterUserViaOrganizationInviteToken_MissingOrInvalidOrgInviteDataWithDisabledOpenRegistration_ThrowsBadRequestException(string scenario,
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, OrganizationUser orgUser, string orgInviteToken, Guid? orgUserId)
    {
        // Arrange
        sutProvider.GetDependency<IGlobalSettings>()
            .DisableUserRegistration.Returns(true);

        switch (scenario)
        {
            case "invalidOrgInviteToken":
                orgUser.Email = null; // make org user not match user and thus make tokenable invalid
                var orgInviteTokenable = new OrgUserInviteTokenable(orgUser);

                sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>()
                    .TryUnprotect(orgInviteToken, out Arg.Any<OrgUserInviteTokenable>())
                    .Returns(callInfo =>
                    {
                        callInfo[1] = orgInviteTokenable;
                        return true;
                    });
                break;
            case "nullOrgInviteToken":
                orgInviteToken = null;
                break;
            case "nullOrgUserId":
                orgUserId = default;
                break;
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, orgInviteToken, orgUserId));
        Assert.Equal("Open registration has been disabled by the system administrator.", exception.Message);
    }

    [Theory]
    [BitAutoData("invalidOrgInviteToken")]
    [BitAutoData("nullOrgInviteToken")]
    [BitAutoData("nullOrgUserId")]
    public async Task RegisterUserViaOrganizationInviteToken_MissingOrInvalidOrgInviteDataWithEnabledOpenRegistration_ThrowsBadRequestException(string scenario,
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, OrganizationUser orgUser, string orgInviteToken, Guid? orgUserId)
    {
        // Arrange
        sutProvider.GetDependency<IGlobalSettings>()
            .DisableUserRegistration.Returns(false);

        string expectedErrorMessage = null;
        switch (scenario)
        {
            case "invalidOrgInviteToken":
                orgUser.Email = null; // make org user not match user and thus make tokenable invalid
                var orgInviteTokenable = new OrgUserInviteTokenable(orgUser);

                sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>()
                    .TryUnprotect(orgInviteToken, out Arg.Any<OrgUserInviteTokenable>())
                    .Returns(callInfo =>
                    {
                        callInfo[1] = orgInviteTokenable;
                        return true;
                    });

                expectedErrorMessage = "Organization invite token is invalid.";
                break;
            case "nullOrgInviteToken":
                orgInviteToken = null;
                expectedErrorMessage = "Organization user id cannot be provided without an organization invite token.";
                break;
            case "nullOrgUserId":
                orgUserId = default;
                expectedErrorMessage = "Organization invite token cannot be validated without an organization user id.";
                break;
        }

        user.ReferenceData = null;

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, orgInviteToken, orgUserId));
        Assert.Equal(expectedErrorMessage, exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaOrganizationInviteToken_BlockedDomainFromDifferentOrg_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, OrganizationUser orgUser, string orgInviteToken, Guid orgUserId)
    {
        // Arrange
        user.Email = "user@blocked-domain.com";
        orgUser.Email = user.Email;
        orgUser.Id = orgUserId;
        var blockingOrganizationId = Guid.NewGuid(); // Different org that has the domain blocked
        orgUser.OrganizationId = Guid.NewGuid(); // The org they're trying to join

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser);

        sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>()
            .TryUnprotect(orgInviteToken, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = orgInviteTokenable;
                return true;
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        // Mock the new overload that excludes the organization - it should return true (domain IS blocked by another org)
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("blocked-domain.com", orgUser.OrganizationId)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, orgInviteToken, orgUserId));
        Assert.Equal("This email address is claimed by an organization using Bitwarden.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaOrganizationInviteToken_BlockedDomainFromSameOrg_Succeeds(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, OrganizationUser orgUser, string orgInviteToken, Guid orgUserId)
    {
        // Arrange
        user.Email = "user@company-domain.com";
        user.ReferenceData = null;
        orgUser.Email = user.Email;
        orgUser.Id = orgUserId;
        // The organization owns the domain and is trying to invite the user
        orgUser.OrganizationId = Guid.NewGuid();

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser);

        sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>()
            .TryUnprotect(orgInviteToken, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = orgInviteTokenable;
                return true;
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUserId)
            .Returns(orgUser);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        // Mock the new overload - it should return false (domain is NOT blocked by OTHER orgs)
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("company-domain.com", orgUser.OrganizationId)
            .Returns(false);

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        // Act
        var result = await sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, orgInviteToken, orgUserId);

        // Assert
        Assert.True(result.Succeeded);
        await sutProvider.GetDependency<IOrganizationDomainRepository>()
            .Received(1)
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("company-domain.com", orgUser.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaOrganizationInviteToken_WithValidTokenButNullOrgUser_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, OrganizationUser orgUser, string orgInviteToken, Guid orgUserId)
    {
        // Arrange
        user.Email = "user@example.com";
        orgUser.Email = user.Email;
        orgUser.Id = orgUserId;

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser);

        sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>()
            .TryUnprotect(orgInviteToken, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = orgInviteTokenable;
                return true;
            });

        // Mock GetByIdAsync to return null - simulating a deleted or non-existent organization user
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUserId)
            .Returns((OrganizationUser)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, orgInviteToken, orgUserId));
        Assert.Equal("Invalid organization user invitation.", exception.Message);

        // Verify that GetByIdAsync was called
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetByIdAsync(orgUserId);

        // Verify that user creation was never attempted
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceive()
            .CreateUserAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    // -----------------------------------------------------------------------------------------------
    // RegisterUserViaEmailVerificationToken tests
    // -----------------------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaEmailVerificationToken_Succeeds(SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, string emailVerificationToken, bool receiveMarketingMaterials)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .TryUnprotect(emailVerificationToken, out Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new RegistrationEmailVerificationTokenable(user.Email, user.Name, receiveMarketingMaterials);
                return true;
            });

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        // Act
        var result = await sutProvider.Sut.RegisterUserViaEmailVerificationToken(user, masterPasswordHash, emailVerificationToken);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(Arg.Is<User>(u => u.Name == user.Name && u.EmailVerified == true && u.ApiKey != null), masterPasswordHash);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendWelcomeEmailAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaEmailVerificationToken_InvalidToken_ThrowsBadRequestException(SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, string emailVerificationToken, bool receiveMarketingMaterials)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .TryUnprotect(emailVerificationToken, out Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new RegistrationEmailVerificationTokenable("wrongEmail@test.com", user.Name, receiveMarketingMaterials);
                return true;
            });

        // Act & Assert
        var result = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegisterUserViaEmailVerificationToken(user, masterPasswordHash, emailVerificationToken));
        Assert.Equal("Invalid email verification token.", result.Message);

    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaEmailVerificationToken_DisabledOpenRegistration_ThrowsBadRequestException(SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, string emailVerificationToken)
    {
        // Arrange
        sutProvider.GetDependency<IGlobalSettings>()
            .DisableUserRegistration = true;

        // Act & Assert
        var result = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.RegisterUserViaEmailVerificationToken(user, masterPasswordHash, emailVerificationToken));
        Assert.Equal("Open registration has been disabled by the system administrator.", result.Message);

    }

    // -----------------------------------------------------------------------------------------------
    // RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken tests
    // -----------------------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken_Succeeds(SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash,
        string orgSponsoredFreeFamilyPlanInviteToken)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IValidateRedemptionTokenCommand>()
            .ValidateRedemptionTokenAsync(orgSponsoredFreeFamilyPlanInviteToken, user.Email)
            .Returns((true, new OrganizationSponsorship()));

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        // Act
        var result = await sutProvider.Sut.RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(user, masterPasswordHash, orgSponsoredFreeFamilyPlanInviteToken);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(Arg.Is<User>(u => u.Name == user.Name && u.EmailVerified == true && u.ApiKey != null), masterPasswordHash);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendWelcomeEmailAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken_InvalidToken_ThrowsBadRequestException(SutProvider<RegisterUserCommand> sutProvider, User user,
        string masterPasswordHash, string orgSponsoredFreeFamilyPlanInviteToken)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IValidateRedemptionTokenCommand>()
            .ValidateRedemptionTokenAsync(orgSponsoredFreeFamilyPlanInviteToken, user.Email)
            .Returns((false, new OrganizationSponsorship()));

        // Act & Assert
        var result = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(user, masterPasswordHash, orgSponsoredFreeFamilyPlanInviteToken));
        Assert.Equal("Invalid org sponsored free family plan token.", result.Message);

    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken_DisabledOpenRegistration_ThrowsBadRequestException(SutProvider<RegisterUserCommand> sutProvider, User user,
        string masterPasswordHash, string orgSponsoredFreeFamilyPlanInviteToken)
    {
        // Arrange
        sutProvider.GetDependency<IGlobalSettings>()
            .DisableUserRegistration = true;

        // Act & Assert
        var result = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(user, masterPasswordHash, orgSponsoredFreeFamilyPlanInviteToken));
        Assert.Equal("Open registration has been disabled by the system administrator.", result.Message);
    }

    // -----------------------------------------------------------------------------------------------
    // RegisterUserViaAcceptEmergencyAccessInviteToken tests
    // -----------------------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaAcceptEmergencyAccessInviteToken_Succeeds(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash,
        EmergencyAccess emergencyAccess, string acceptEmergencyAccessInviteToken, Guid acceptEmergencyAccessId)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";
        emergencyAccess.Email = user.Email;
        emergencyAccess.Id = acceptEmergencyAccessId;

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
            .TryUnprotect(acceptEmergencyAccessInviteToken, out Arg.Any<EmergencyAccessInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new EmergencyAccessInviteTokenable(emergencyAccess, 10);
                return true;
            });

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        // Act
        var result = await sutProvider.Sut.RegisterUserViaAcceptEmergencyAccessInviteToken(user, masterPasswordHash, acceptEmergencyAccessInviteToken, acceptEmergencyAccessId);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(Arg.Is<User>(u => u.Name == user.Name && u.EmailVerified == true && u.ApiKey != null), masterPasswordHash);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendWelcomeEmailAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaAcceptEmergencyAccessInviteToken_InvalidToken_ThrowsBadRequestException(SutProvider<RegisterUserCommand> sutProvider, User user,
        string masterPasswordHash, EmergencyAccess emergencyAccess, string acceptEmergencyAccessInviteToken, Guid acceptEmergencyAccessId)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";
        emergencyAccess.Email = "wrong@email.com";
        emergencyAccess.Id = acceptEmergencyAccessId;

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
            .TryUnprotect(acceptEmergencyAccessInviteToken, out Arg.Any<EmergencyAccessInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new EmergencyAccessInviteTokenable(emergencyAccess, 10);
                return true;
            });

        // Act & Assert
        var result = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaAcceptEmergencyAccessInviteToken(user, masterPasswordHash, acceptEmergencyAccessInviteToken, acceptEmergencyAccessId));
        Assert.Equal("Invalid accept emergency access invite token.", result.Message);

    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaAcceptEmergencyAccessInviteToken_DisabledOpenRegistration_ThrowsBadRequestException(SutProvider<RegisterUserCommand> sutProvider, User user,
        string masterPasswordHash, string acceptEmergencyAccessInviteToken, Guid acceptEmergencyAccessId)
    {
        // Arrange
        sutProvider.GetDependency<IGlobalSettings>()
            .DisableUserRegistration = true;

        // Act & Assert
        var result = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaAcceptEmergencyAccessInviteToken(user, masterPasswordHash, acceptEmergencyAccessInviteToken, acceptEmergencyAccessId));
        Assert.Equal("Open registration has been disabled by the system administrator.", result.Message);
    }

    // -----------------------------------------------------------------------------------------------
    // RegisterUserViaProviderInviteToken tests
    // -----------------------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaProviderInviteToken_Succeeds(SutProvider<RegisterUserCommand> sutProvider,
        User user, string masterPasswordHash, Guid providerUserId)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";

        // Start with plaintext
        var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
        var decryptedProviderInviteToken = $"ProviderUserInvite {providerUserId} {user.Email} {nowMillis}";

        // Get the byte array of the plaintext
        var decryptedProviderInviteTokenByteArray = Encoding.UTF8.GetBytes(decryptedProviderInviteToken);

        // Base64 encode the byte array (this is passed to protector.protect(bytes))
        var base64EncodedProviderInvToken = WebEncoders.Base64UrlEncode(decryptedProviderInviteTokenByteArray);

        var mockDataProtector = Substitute.For<IDataProtector>();

        // Given any byte array, just return the decryptedProviderInviteTokenByteArray (sidestepping any actual encryption)
        mockDataProtector.Unprotect(Arg.Any<byte[]>()).Returns(decryptedProviderInviteTokenByteArray);

        sutProvider.GetDependency<IDataProtectionProvider>()
            .CreateProtector("ProviderServiceDataProtector")
            .Returns(mockDataProtector);

        sutProvider.GetDependency<IGlobalSettings>()
            .OrganizationInviteExpirationHours.Returns(120); // 5 days

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        // Using sutProvider in the parameters of the function means that the constructor has already run for the
        // command so we have to recreate it in order for our mock overrides to be used.
        sutProvider.Create();

        // Act
        var result = await sutProvider.Sut.RegisterUserViaProviderInviteToken(user, masterPasswordHash, base64EncodedProviderInvToken, providerUserId);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IUserService>()
            .Received(1)
            .CreateUserAsync(Arg.Is<User>(u => u.Name == user.Name && u.EmailVerified == true && u.ApiKey != null), masterPasswordHash);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendWelcomeEmailAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaProviderInviteToken_InvalidToken_ThrowsBadRequestException(SutProvider<RegisterUserCommand> sutProvider,
        User user, string masterPasswordHash, Guid providerUserId)
    {
        // Arrange
        user.Email = $"test+{Guid.NewGuid()}@example.com";

        // Start with plaintext
        var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
        var decryptedProviderInviteToken = $"ProviderUserInvite {providerUserId} {user.Email} {nowMillis}";

        // Get the byte array of the plaintext
        var decryptedProviderInviteTokenByteArray = Encoding.UTF8.GetBytes(decryptedProviderInviteToken);

        // Base64 encode the byte array (this is passed to protector.protect(bytes))
        var base64EncodedProviderInvToken = WebEncoders.Base64UrlEncode(decryptedProviderInviteTokenByteArray);

        var mockDataProtector = Substitute.For<IDataProtector>();

        // Given any byte array, just return the decryptedProviderInviteTokenByteArray (sidestepping any actual encryption)
        mockDataProtector.Unprotect(Arg.Any<byte[]>()).Returns(decryptedProviderInviteTokenByteArray);

        sutProvider.GetDependency<IDataProtectionProvider>()
            .CreateProtector("ProviderServiceDataProtector")
            .Returns(mockDataProtector);

        sutProvider.GetDependency<IGlobalSettings>()
            .OrganizationInviteExpirationHours.Returns(120); // 5 days

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>())
            .Returns(false);

        // Using sutProvider in the parameters of the function means that the constructor has already run for the
        // command so we have to recreate it in order for our mock overrides to be used.
        sutProvider.Create();

        // Act & Assert
        var result = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaProviderInviteToken(user, masterPasswordHash, base64EncodedProviderInvToken, Guid.NewGuid()));
        Assert.Equal("Invalid provider invite token.", result.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaProviderInviteToken_DisabledOpenRegistration_ThrowsBadRequestException(SutProvider<RegisterUserCommand> sutProvider,
        User user, string masterPasswordHash, Guid providerUserId)
    {
        // Arrange
        // Start with plaintext
        var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
        var decryptedProviderInviteToken = $"ProviderUserInvite {providerUserId} {user.Email} {nowMillis}";

        // Get the byte array of the plaintext
        var decryptedProviderInviteTokenByteArray = Encoding.UTF8.GetBytes(decryptedProviderInviteToken);

        // Base64 encode the byte array (this is passed to protector.protect(bytes))
        var base64EncodedProviderInvToken = WebEncoders.Base64UrlEncode(decryptedProviderInviteTokenByteArray);

        var mockDataProtector = Substitute.For<IDataProtector>();

        // Given any byte array, just return the decryptedProviderInviteTokenByteArray (sidestepping any actual encryption)
        mockDataProtector.Unprotect(Arg.Any<byte[]>()).Returns(decryptedProviderInviteTokenByteArray);

        sutProvider.GetDependency<IDataProtectionProvider>()
            .CreateProtector("ProviderServiceDataProtector")
            .Returns(mockDataProtector);

        sutProvider.GetDependency<IGlobalSettings>()
            .DisableUserRegistration = true;

        // Using sutProvider in the parameters of the function means that the constructor has already run for the
        // command so we have to recreate it in order for our mock overrides to be used.
        sutProvider.Create();

        // Act & Assert
        var result = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaProviderInviteToken(user, masterPasswordHash, base64EncodedProviderInvToken, providerUserId));
        Assert.Equal("Open registration has been disabled by the system administrator.", result.Message);
    }

    // -----------------------------------------------------------------------------------------------
    // Domain blocking tests (BlockClaimedDomainAccountCreation policy)
    // -----------------------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RegisterUser_BlockedDomain_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user)
    {
        // Arrange
        user.Email = "user@blocked-domain.com";

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("blocked-domain.com")
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUser(user));
        Assert.Equal("This email address is claimed by an organization using Bitwarden.", exception.Message);

        // Verify user creation was never attempted
        await sutProvider.GetDependency<IUserService>()
            .DidNotReceive()
            .CreateUserAsync(Arg.Any<User>());
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUser_AllowedDomain_Succeeds(
        SutProvider<RegisterUserCommand> sutProvider, User user)
    {
        // Arrange
        user.Email = "user@allowed-domain.com";

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("allowed-domain.com")
            .Returns(false);

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Success);

        // Act
        var result = await sutProvider.Sut.RegisterUser(user);

        // Assert
        Assert.True(result.Succeeded);
        await sutProvider.GetDependency<IOrganizationDomainRepository>()
            .Received(1)
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("allowed-domain.com");
    }

    // SendWelcomeEmail tests
    // -----------------------------------------------------------------------------------------------
    [Theory]
    [BitAutoData(PlanType.FamiliesAnnually)]
    [BitAutoData(PlanType.FamiliesAnnually2019)]
    [BitAutoData(PlanType.Free)]
    public async Task SendWelcomeEmail_FamilyOrg_SendsFamilyWelcomeEmail(
        PlanType planType,
        User user,
        Organization organization,
        SutProvider<RegisterUserCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = planType;
        organization.Name = "Family Org";

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((OrganizationUser)null);

        // Act
        await sutProvider.Sut.RegisterSSOAutoProvisionedUserAsync(user, organization);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendFreeOrgOrFamilyOrgUserWelcomeEmailAsync(user, organization.Name);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaEmailVerificationToken_BlockedDomain_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash,
        string emailVerificationToken, bool receiveMarketingMaterials)
    {
        // Arrange
        user.Email = "user@blocked-domain.com";

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("blocked-domain.com")
            .Returns(true);

        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .TryUnprotect(emailVerificationToken, out Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new RegistrationEmailVerificationTokenable(user.Email, user.Name, receiveMarketingMaterials);
                return true;
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaEmailVerificationToken(user, masterPasswordHash, emailVerificationToken));
        Assert.Equal("This email address is claimed by an organization using Bitwarden.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken_BlockedDomain_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash,
        string orgSponsoredFreeFamilyPlanInviteToken)
    {
        // Arrange
        user.Email = "user@blocked-domain.com";

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("blocked-domain.com")
            .Returns(true);

        sutProvider.GetDependency<IValidateRedemptionTokenCommand>()
            .ValidateRedemptionTokenAsync(orgSponsoredFreeFamilyPlanInviteToken, user.Email)
            .Returns((true, new OrganizationSponsorship()));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaOrganizationSponsoredFreeFamilyPlanInviteToken(user, masterPasswordHash, orgSponsoredFreeFamilyPlanInviteToken));
        Assert.Equal("This email address is claimed by an organization using Bitwarden.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaAcceptEmergencyAccessInviteToken_BlockedDomain_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash,
        EmergencyAccess emergencyAccess, string acceptEmergencyAccessInviteToken, Guid acceptEmergencyAccessId)
    {
        // Arrange
        user.Email = "user@blocked-domain.com";
        emergencyAccess.Email = user.Email;
        emergencyAccess.Id = acceptEmergencyAccessId;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("blocked-domain.com")
            .Returns(true);

        sutProvider.GetDependency<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>()
            .TryUnprotect(acceptEmergencyAccessInviteToken, out Arg.Any<EmergencyAccessInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new EmergencyAccessInviteTokenable(emergencyAccess, 10);
                return true;
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaAcceptEmergencyAccessInviteToken(user, masterPasswordHash, acceptEmergencyAccessInviteToken, acceptEmergencyAccessId));
        Assert.Equal("This email address is claimed by an organization using Bitwarden.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaProviderInviteToken_BlockedDomain_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash, Guid providerUserId)
    {
        // Arrange
        user.Email = "user@blocked-domain.com";

        // Start with plaintext
        var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
        var decryptedProviderInviteToken = $"ProviderUserInvite {providerUserId} {user.Email} {nowMillis}";

        // Get the byte array of the plaintext
        var decryptedProviderInviteTokenByteArray = Encoding.UTF8.GetBytes(decryptedProviderInviteToken);

        // Base64 encode the byte array (this is passed to protector.protect(bytes))
        var base64EncodedProviderInvToken = WebEncoders.Base64UrlEncode(decryptedProviderInviteTokenByteArray);

        var mockDataProtector = Substitute.For<IDataProtector>();

        // Given any byte array, just return the decryptedProviderInviteTokenByteArray (sidestepping any actual encryption)
        mockDataProtector.Unprotect(Arg.Any<byte[]>()).Returns(decryptedProviderInviteTokenByteArray);

        sutProvider.GetDependency<IDataProtectionProvider>()
            .CreateProtector("ProviderServiceDataProtector")
            .Returns(mockDataProtector);

        sutProvider.GetDependency<IGlobalSettings>()
            .OrganizationInviteExpirationHours.Returns(120); // 5 days

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("blocked-domain.com")
            .Returns(true);

        // Using sutProvider in the parameters of the function means that the constructor has already run for the
        // command so we have to recreate it in order for our mock overrides to be used.
        sutProvider.Create();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaProviderInviteToken(user, masterPasswordHash, base64EncodedProviderInvToken, providerUserId));
        Assert.Equal("This email address is claimed by an organization using Bitwarden.", exception.Message);
    }

    // -----------------------------------------------------------------------------------------------
    // Invalid email format tests
    // -----------------------------------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RegisterUser_InvalidEmailFormat_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user)
    {
        // Arrange
        user.Email = "invalid-email-format";

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUser(user));
        Assert.Equal("Invalid email address format.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RegisterUserViaEmailVerificationToken_InvalidEmailFormat_ThrowsBadRequestException(
        SutProvider<RegisterUserCommand> sutProvider, User user, string masterPasswordHash,
        string emailVerificationToken, bool receiveMarketingMaterials)
    {
        // Arrange
        user.Email = "invalid-email-format";

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>()
            .TryUnprotect(emailVerificationToken, out Arg.Any<RegistrationEmailVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = new RegistrationEmailVerificationTokenable(user.Email, user.Name, receiveMarketingMaterials);
                return true;
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterUserViaEmailVerificationToken(user, masterPasswordHash, emailVerificationToken));
        Assert.Equal("Invalid email address format.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SendWelcomeEmail_OrganizationNull_SendsIndividualWelcomeEmail(
        User user,
        OrganizationUser orgUser,
        string orgInviteToken,
        string masterPasswordHash,
        SutProvider<RegisterUserCommand> sutProvider)
    {
        // Arrange
        user.ReferenceData = null;
        orgUser.Email = user.Email;

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser.Id)
            .Returns(orgUser);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(Arg.Any<Guid>(), PolicyType.TwoFactorAuthentication)
            .Returns((Policy)null);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgUser.OrganizationId)
            .Returns((Organization)null);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(true);

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser);

        sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>()
            .TryUnprotect(orgInviteToken, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = orgInviteTokenable;
                return true;
            });

        // Act
        var result = await sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, orgInviteToken, orgUser.Id);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendIndividualUserWelcomeEmailAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task SendWelcomeEmail_OrganizationDisplayNameNull_SendsIndividualWelcomeEmail(
        User user,
        SutProvider<RegisterUserCommand> sutProvider)
    {
        // Arrange
        Organization organization = new Organization
        {
            Name = null
        };

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(Arg.Any<Guid>())
            .Returns((OrganizationUser)null);

        // Act
        await sutProvider.Sut.RegisterSSOAutoProvisionedUserAsync(user, organization);

        // Assert
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendIndividualUserWelcomeEmailAsync(user);
    }

    [Theory]
    [BitAutoData]
    public async Task GetOrganizationWelcomeEmailDetailsAsync_HappyPath_ReturnsOrganizationWelcomeEmailDetails(
        Organization organization,
        User user,
        OrganizationUser orgUser,
        string masterPasswordHash,
        string orgInviteToken,
        SutProvider<RegisterUserCommand> sutProvider)
    {
        // Arrange
        user.ReferenceData = null;
        orgUser.Email = user.Email;
        organization.PlanType = PlanType.EnterpriseAnnually;

        sutProvider.GetDependency<IUserService>()
            .CreateUserAsync(user, masterPasswordHash)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUser.Id)
            .Returns(orgUser);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(Arg.Any<Guid>(), PolicyType.TwoFactorAuthentication)
            .Returns((Policy)null);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgUser.OrganizationId)
            .Returns(organization);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.MjmlWelcomeEmailTemplates)
            .Returns(true);

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser);

        sutProvider.GetDependency<IDataProtectorTokenFactory<OrgUserInviteTokenable>>()
            .TryUnprotect(orgInviteToken, out Arg.Any<OrgUserInviteTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = orgInviteTokenable;
                return true;
            });

        // Act
        var result = await sutProvider.Sut.RegisterUserViaOrganizationInviteToken(user, masterPasswordHash, orgInviteToken, orgUser.Id);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .GetByIdAsync(orgUser.OrganizationId);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserWelcomeEmailAsync(user, organization.DisplayName());
    }
}
