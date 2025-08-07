using System.Security.Claims;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.Helpers;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class UserServiceTests
{
    [Theory, BitAutoData]
    public async Task SaveUserAsync_SetsNameToNull_WhenNameIsEmpty(SutProvider<UserService> sutProvider, User user)
    {
        user.Name = string.Empty;
        await sutProvider.Sut.SaveUserAsync(user);
        Assert.Null(user.Name);
    }

    [Theory, BitAutoData]
    public async Task UpdateLicenseAsync_Success(SutProvider<UserService> sutProvider,
        User user, UserLicense userLicense)
    {
        using var tempDir = new TempDirectory();

        var now = DateTime.UtcNow;
        userLicense.Issued = now.AddDays(-10);
        userLicense.Expires = now.AddDays(10);
        userLicense.Version = 1;
        userLicense.Premium = true;

        user.EmailVerified = true;
        user.Email = userLicense.Email;

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IGlobalSettings>().LicenseDirectory = tempDir.Directory;
        sutProvider.GetDependency<ILicensingService>()
            .VerifyLicense(userLicense)
            .Returns(true);
        sutProvider.GetDependency<ILicensingService>()
            .GetClaimsPrincipalFromLicense(userLicense)
            .Returns((ClaimsPrincipal)null);

        await sutProvider.Sut.UpdateLicenseAsync(user, userLicense);

        var filePath = Path.Combine(tempDir.Directory, "user", $"{user.Id}.json");
        Assert.True(File.Exists(filePath));
        var document = JsonDocument.Parse(File.OpenRead(filePath));
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        // Sort of a lazy way to test that it is indented but not sure of a better way
        Assert.Contains('\n', root.GetRawText());
        AssertHelper.AssertJsonProperty(root, "LicenseKey", JsonValueKind.String);
        AssertHelper.AssertJsonProperty(root, "Id", JsonValueKind.String);
        AssertHelper.AssertJsonProperty(root, "Premium", JsonValueKind.True);
        var versionProp = AssertHelper.AssertJsonProperty(root, "Version", JsonValueKind.Number);
        Assert.Equal(1, versionProp.GetInt32());
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganization_Returns_False_If_No_Orgs(SutProvider<UserService> sutProvider, User user)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id).Returns(new List<OrganizationUser>());
        Assert.False(await sutProvider.Sut.HasPremiumFromOrganization(user));

    }

    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    public async Task HasPremiumFromOrganization_Returns_False_If_Org_Not_Eligible(bool orgEnabled, bool orgUsersGetPremium, SutProvider<UserService> sutProvider, User user, OrganizationUser orgUser, Organization organization)
    {
        orgUser.OrganizationId = organization.Id;
        organization.Enabled = orgEnabled;
        organization.UsersGetPremium = orgUsersGetPremium;
        var orgAbilities = OrganizationAbilityBuilder.BuildConcurrentDictionary(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id).Returns(new List<OrganizationUser>() { orgUser });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(orgAbilities);

        Assert.False(await sutProvider.Sut.HasPremiumFromOrganization(user));
    }

    [Theory, BitAutoData]
    public async Task HasPremiumFromOrganization_Returns_True_If_Org_Eligible(SutProvider<UserService> sutProvider, User user, OrganizationUser orgUser, Organization organization)
    {
        orgUser.OrganizationId = organization.Id;
        organization.Enabled = true;
        organization.UsersGetPremium = true;
        var orgAbilities = OrganizationAbilityBuilder.BuildConcurrentDictionary(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id).Returns(new List<OrganizationUser>() { orgUser });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(orgAbilities);

        Assert.True(await sutProvider.Sut.HasPremiumFromOrganization(user));
    }


    [Flags]
    public enum ShouldCheck
    {
        Password = 0x1,
        OTP = 0x2,
    }

    [Theory]
    // A user who has a password, and the password is valid should only check for that password
    [BitAutoData(true, "test_password", true, ShouldCheck.Password)]
    // A user who does not have a password, should only check if the OTP is valid
    [BitAutoData(false, "otp_token", true, ShouldCheck.OTP)]
    // A user who has a password but supplied a OTP, it will check password first and then try OTP
    [BitAutoData(true, "otp_token", true, ShouldCheck.Password | ShouldCheck.OTP)]
    // A user who does not have a password and supplied an invalid OTP token, should only check OTP and return invalid
    [BitAutoData(false, "bad_otp_token", false, ShouldCheck.OTP)]
    // A user who does have a password but they supply a bad one, we will check both but it will still be invalid
    [BitAutoData(true, "bad_test_password", false, ShouldCheck.Password | ShouldCheck.OTP)]
    public async Task VerifySecretAsync_Works(
        bool shouldHavePassword, string secret, bool expectedIsVerified, ShouldCheck shouldCheck, // inline theory data
        User user) // AutoFixture injected data
    {
        // Arrange
        SetupUserAndDevice(user, shouldHavePassword);

        var sutProvider = new SutProvider<UserService>()
            .CreateWithUserServiceCustomizations(user);

        // Setup the fake password verification
        sutProvider.GetDependency<IUserPasswordStore<User>>()
            .GetPasswordHashAsync(user, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("hashed_test_password"));

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .VerifyHashedPassword(user, "hashed_test_password", "test_password")
            .Returns(PasswordVerificationResult.Success);

        var actualIsVerified = await sutProvider.Sut.VerifySecretAsync(user, secret);

        Assert.Equal(expectedIsVerified, actualIsVerified);

        await sutProvider.GetDependency<IUserTwoFactorTokenProvider<User>>()
            .Received(shouldCheck.HasFlag(ShouldCheck.OTP) ? 1 : 0)
            .ValidateAsync(Arg.Any<string>(), secret, Arg.Any<UserManager<User>>(), user);

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .Received(shouldCheck.HasFlag(ShouldCheck.Password) ? 1 : 0)
            .VerifyHashedPassword(user, "hashed_test_password", secret);
    }

    [Theory, BitAutoData]
    public async Task IsClaimedByAnyOrganizationAsync_WithManagingEnabledOrganization_ReturnsTrue(
        SutProvider<UserService> sutProvider, Guid userId, Organization organization)
    {
        organization.Enabled = true;
        organization.UseOrganizationDomains = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(userId)
            .Returns(new[] { organization });

        var result = await sutProvider.Sut.IsClaimedByAnyOrganizationAsync(userId);
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task IsClaimedByAnyOrganizationAsync_WithManagingDisabledOrganization_ReturnsFalse(
        SutProvider<UserService> sutProvider, Guid userId, Organization organization)
    {
        organization.Enabled = false;
        organization.UseOrganizationDomains = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(userId)
            .Returns(new[] { organization });

        var result = await sutProvider.Sut.IsClaimedByAnyOrganizationAsync(userId);
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task IsClaimedByAnyOrganizationAsync_WithOrganizationUseOrganizationDomaisFalse_ReturnsFalse(
        SutProvider<UserService> sutProvider, Guid userId, Organization organization)
    {
        organization.Enabled = true;
        organization.UseOrganizationDomains = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByVerifiedUserEmailDomainAsync(userId)
            .Returns(new[] { organization });

        var result = await sutProvider.Sut.IsClaimedByAnyOrganizationAsync(userId);
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task DisableTwoFactorProviderAsync_WhenOrganizationHas2FAPolicyEnabled_DisablingAllProviders_RevokesUserAndSendsEmail(
        SutProvider<UserService> sutProvider, User user,
        Organization organization1, Guid organizationUserId1,
        Organization organization2, Guid organizationUserId2)
    {
        // Arrange
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new() { Enabled = true }
        });
        organization1.Enabled = organization2.Enabled = true;
        organization1.UseSso = organization2.UseSso = true;

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication)
            .Returns(
            [
                new OrganizationUserPolicyDetails
                {
                    OrganizationId = organization1.Id,
                    OrganizationUserId = organizationUserId1,
                    PolicyType = PolicyType.TwoFactorAuthentication,
                    PolicyEnabled = true
                },
                new OrganizationUserPolicyDetails
                {
                    OrganizationId = organization2.Id,
                    OrganizationUserId = organizationUserId2,
                    PolicyType = PolicyType.TwoFactorAuthentication,
                    PolicyEnabled = true
                }
            ]);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization1.Id)
            .Returns(organization1);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization2.Id)
            .Returns(organization2);
        var expectedSavedProviders = JsonHelpers.LegacySerialize(new Dictionary<TwoFactorProviderType, TwoFactorProvider>(), JsonHelpers.LegacyEnumKeyResolver);

        // Act
        await sutProvider.Sut.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Email);

        // Assert
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.Id == user.Id && u.TwoFactorProviders == expectedSavedProviders));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogUserEventAsync(user.Id, EventType.User_Disabled2fa);

        // Revoke the user from the first organization
        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received(1)
            .RevokeNonCompliantOrganizationUsersAsync(
                Arg.Is<RevokeOrganizationUsersRequest>(r => r.OrganizationId == organization1.Id &&
                    r.OrganizationUsers.First().Id == organizationUserId1 &&
                    r.OrganizationUsers.First().OrganizationId == organization1.Id));
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization1.DisplayName(), user.Email);

        // Remove the user from the second organization
        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received(1)
            .RevokeNonCompliantOrganizationUsersAsync(
                Arg.Is<RevokeOrganizationUsersRequest>(r => r.OrganizationId == organization2.Id &&
                    r.OrganizationUsers.First().Id == organizationUserId2 &&
                    r.OrganizationUsers.First().OrganizationId == organization2.Id));
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization2.DisplayName(), user.Email);
    }

    [Theory, BitAutoData]
    public async Task DisableTwoFactorProviderAsync_WithPolicyRequirementsEnabled_WhenOrganizationHas2FAPolicyEnabled_DisablingAllProviders_RevokesUserAndSendsEmail(
        SutProvider<UserService> sutProvider, User user,
        Organization organization1, Guid organizationUserId1,
        Organization organization2, Guid organizationUserId2)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new() { Enabled = true }
        });
        organization1.Enabled = organization2.Enabled = true;
        organization1.UseSso = organization2.UseSso = true;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organization1.Id,
                    OrganizationUserId = organizationUserId1,
                    OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                    PolicyType = PolicyType.TwoFactorAuthentication
                },
                new PolicyDetails
                {
                    OrganizationId = organization2.Id,
                    OrganizationUserId = organizationUserId2,
                    OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]));
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organization1.Id) && ids.Contains(organization2.Id)))
            .Returns(new[] { organization1, organization2 });
        var expectedSavedProviders = JsonHelpers.LegacySerialize(new Dictionary<TwoFactorProviderType, TwoFactorProvider>(), JsonHelpers.LegacyEnumKeyResolver);

        await sutProvider.Sut.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Email);

        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.Id == user.Id && u.TwoFactorProviders == expectedSavedProviders));
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogUserEventAsync(user.Id, EventType.User_Disabled2fa);

        // Revoke the user from the first organization
        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received(1)
            .RevokeNonCompliantOrganizationUsersAsync(
                Arg.Is<RevokeOrganizationUsersRequest>(r => r.OrganizationId == organization1.Id &&
                    r.OrganizationUsers.First().Id == organizationUserId1 &&
                    r.OrganizationUsers.First().OrganizationId == organization1.Id));
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization1.DisplayName(), user.Email);

        // Remove the user from the second organization
        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received(1)
            .RevokeNonCompliantOrganizationUsersAsync(
                Arg.Is<RevokeOrganizationUsersRequest>(r => r.OrganizationId == organization2.Id &&
                    r.OrganizationUsers.First().Id == organizationUserId2 &&
                    r.OrganizationUsers.First().OrganizationId == organization2.Id));
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization2.DisplayName(), user.Email);
    }

    [Theory, BitAutoData]
    public async Task DisableTwoFactorProviderAsync_UserHasOneProviderEnabled_DoesNotRevokeUserFromOrganization(
        SutProvider<UserService> sutProvider, User user, Organization organization)
    {
        // Arrange
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new() { Enabled = true },
            [TwoFactorProviderType.Remember] = new() { Enabled = true }
        });
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication)
            .Returns(
            [
                new OrganizationUserPolicyDetails
                {
                    OrganizationId = organization.Id,
                    PolicyType = PolicyType.TwoFactorAuthentication,
                    PolicyEnabled = true
                }
            ]);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(true);
        var expectedSavedProviders = JsonHelpers.LegacySerialize(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Remember] = new() { Enabled = true }
        }, JsonHelpers.LegacyEnumKeyResolver);

        // Act
        await sutProvider.Sut.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Email);

        // Assert
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.Id == user.Id && u.TwoFactorProviders == expectedSavedProviders));
        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .RevokeNonCompliantOrganizationUsersAsync(default);
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceiveWithAnyArgs()
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DisableTwoFactorProviderAsync_WithPolicyRequirementsEnabled_UserHasOneProviderEnabled_DoesNotRevokeUserFromOrganization(
        SutProvider<UserService> sutProvider, User user, Organization organization)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Email] = new() { Enabled = true },
            [TwoFactorProviderType.Remember] = new() { Enabled = true }
        });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organization.Id,
                    OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]));
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(user)
            .Returns(true);
        var expectedSavedProviders = JsonHelpers.LegacySerialize(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.Remember] = new() { Enabled = true }
        }, JsonHelpers.LegacyEnumKeyResolver);

        await sutProvider.Sut.DisableTwoFactorProviderAsync(user, TwoFactorProviderType.Email);

        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.Id == user.Id && u.TwoFactorProviders == expectedSavedProviders));
        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .RevokeNonCompliantOrganizationUsersAsync(default);
        await sutProvider.GetDependency<IMailService>()
            .DidNotReceiveWithAnyArgs()
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(default, default);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("null")]
    public async Task SendOTPAsync_UserEmailNull_ThrowsBadRequest(
        string email,
        SutProvider<UserService> sutProvider, User user)
    {
        user.Email = email == "null" ? null : "";
        var expectedMessage = "No user email.";
        try
        {
            await sutProvider.Sut.SendOTPAsync(user);
        }
        catch (BadRequestException ex)
        {
            Assert.Equal(ex.Message, expectedMessage);
            await sutProvider.GetDependency<IMailService>()
                .DidNotReceive()
                .SendOTPEmailAsync(Arg.Any<string>(), Arg.Any<string>());
        }
    }

    [Theory, BitAutoData]
    public async Task ActiveNewDeviceVerificationException_UserNotInCache_ReturnsFalseAsync(
        SutProvider<UserService> sutProvider)
    {
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>())
            .Returns(null as byte[]);

        var result = await sutProvider.Sut.ActiveNewDeviceVerificationException(Guid.NewGuid());

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ActiveNewDeviceVerificationException_UserInCache_ReturnsTrueAsync(
        SutProvider<UserService> sutProvider)
    {
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>())
            .Returns([1]);

        var result = await sutProvider.Sut.ActiveNewDeviceVerificationException(Guid.NewGuid());

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task ToggleNewDeviceVerificationException_UserInCache_RemovesUserFromCache(
        SutProvider<UserService> sutProvider)
    {
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>())
            .Returns([1]);

        await sutProvider.Sut.ToggleNewDeviceVerificationException(Guid.NewGuid());

        await sutProvider.GetDependency<IDistributedCache>()
                .DidNotReceive()
                .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
        await sutProvider.GetDependency<IDistributedCache>()
                .Received(1)
                .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ToggleNewDeviceVerificationException_UserNotInCache_AddsUserToCache(
        SutProvider<UserService> sutProvider)
    {
        sutProvider.GetDependency<IDistributedCache>()
            .GetAsync(Arg.Any<string>())
            .Returns(null as byte[]);

        await sutProvider.Sut.ToggleNewDeviceVerificationException(Guid.NewGuid());

        await sutProvider.GetDependency<IDistributedCache>()
                .Received(1)
                .SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>());
        await sutProvider.GetDependency<IDistributedCache>()
                .DidNotReceive()
                .RemoveAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task RecoverTwoFactorAsync_CorrectCode_ReturnsTrueAndProcessesPolicies(
        User user, SutProvider<UserService> sutProvider)
    {
        // Arrange
        var recoveryCode = "1234";
        user.TwoFactorRecoveryCode = recoveryCode;

        // Act
        var response = await sutProvider.Sut.RecoverTwoFactorAsync(user, recoveryCode);

        // Assert
        Assert.True(response);
        Assert.Null(user.TwoFactorProviders);
        // Make sure a new code was generated for the user
        Assert.NotEqual(recoveryCode, user.TwoFactorRecoveryCode);
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendRecoverTwoFactorEmail(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogUserEventAsync(user.Id, EventType.User_Recovered2fa);
    }

    [Theory, BitAutoData]
    public async Task RecoverTwoFactorAsync_IncorrectCode_ReturnsFalse(
        User user, SutProvider<UserService> sutProvider)
    {
        // Arrange
        var recoveryCode = "1234";
        user.TwoFactorRecoveryCode = "4567";

        // Act
        var response = await sutProvider.Sut.RecoverTwoFactorAsync(user, recoveryCode);

        // Assert
        Assert.False(response);
        Assert.NotNull(user.TwoFactorProviders);
    }

    private static void SetupUserAndDevice(User user,
        bool shouldHavePassword)
    {
        if (shouldHavePassword)
        {
            user.MasterPassword = "test_password";
        }
        else
        {
            user.MasterPassword = null;
        }
    }
}

public static class UserServiceSutProviderExtensions
{
    /// <summary>
    /// Arranges a fake token provider. Must call as part of a builder pattern that ends in Create(), as it modifies
    /// the SutProvider build chain.
    /// </summary>
    private static SutProvider<UserService> SetFakeTokenProvider(this SutProvider<UserService> sutProvider, User user)
    {
        var fakeUserTwoFactorProvider = Substitute.For<IUserTwoFactorTokenProvider<User>>();

        fakeUserTwoFactorProvider
            .GenerateAsync(Arg.Any<string>(), Arg.Any<UserManager<User>>(), user)
            .Returns("OTP_TOKEN");

        fakeUserTwoFactorProvider
            .ValidateAsync(Arg.Any<string>(), Arg.Is<string>(s => s != "otp_token"), Arg.Any<UserManager<User>>(), user)
            .Returns(false);

        fakeUserTwoFactorProvider
            .ValidateAsync(Arg.Any<string>(), "otp_token", Arg.Any<UserManager<User>>(), user)
            .Returns(true);

        var fakeIdentityOptions = Substitute.For<IOptions<IdentityOptions>>();

        fakeIdentityOptions
            .Value
            .Returns(new IdentityOptions
            {
                Tokens = new TokenOptions
                {
                    ProviderMap = new Dictionary<string, TokenProviderDescriptor>()
                    {
                        ["Email"] = new TokenProviderDescriptor(typeof(IUserTwoFactorTokenProvider<User>))
                        {
                            ProviderInstance = fakeUserTwoFactorProvider,
                        }
                    }
                }
            });

        sutProvider.SetDependency(fakeIdentityOptions);
        // Also set the fake provider dependency so that we can retrieve it easily via GetDependency
        sutProvider.SetDependency(fakeUserTwoFactorProvider);

        return sutProvider;
    }

    /// <summary>
    /// Properly registers IUserPasswordStore as IUserStore so it's injected when the sut is initialized.
    /// </summary>
    /// <param name="sutProvider"></param>
    /// <returns></returns>
    private static SutProvider<UserService> SetUserPasswordStore(this SutProvider<UserService> sutProvider)
    {
        var substitutedUserPasswordStore = Substitute.For<IUserPasswordStore<User>>();

        // IUserPasswordStore must be registered under the IUserStore parameter to be properly injected
        // because this is what the constructor expects
        sutProvider.SetDependency<IUserStore<User>>(substitutedUserPasswordStore);

        // Also store it under its own type for retrieval and configuration
        sutProvider.SetDependency(substitutedUserPasswordStore);

        return sutProvider;
    }

    /// <summary>
    /// This is a hack: when autofixture initializes the sut in sutProvider, it overwrites the public
    /// PasswordHasher property with a new substitute, so it loses the configured sutProvider mock.
    /// This doesn't usually happen because our dependencies are not usually public.
    /// Call this AFTER SutProvider.Create().
    /// </summary>
    private static SutProvider<UserService> FixPasswordHasherBug(this SutProvider<UserService> sutProvider)
    {
        // Get the configured sutProvider mock and assign it back to the public property in the base class
        sutProvider.Sut.PasswordHasher = sutProvider.GetDependency<IPasswordHasher<User>>();
        return sutProvider;
    }

    /// <summary>
    /// A helper that combines all SutProvider configuration usually required for UserService.
    /// Call this instead of SutProvider.Create, after any additional configuration your test needs.
    /// </summary>
    public static SutProvider<UserService> CreateWithUserServiceCustomizations(this SutProvider<UserService> sutProvider, User user)
        => sutProvider
            .SetUserPasswordStore()
            .SetFakeTokenProvider(user)
            .Create()
            .FixPasswordHasherBug();

}
