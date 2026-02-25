using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

[SutProviderCustomize]
public class AdminRecoverAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_Success(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);
        var (authenticationData, unlockData) = CreateValidData(user);
        SetupSuccessfulPasswordUpdate(sutProvider, user, authenticationData.MasterPasswordAuthenticationHash);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData);

        // Assert
        Assert.True(result.Succeeded);
        await AssertSuccessAsync(sutProvider, user, unlockData.MasterKeyWrappedUserKey, organization, organizationUser);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_OrganizationDoesNotExist_ThrowsBadRequest(
        [OrganizationUser] OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns((Organization)null);
        var (authenticationData, unlockData) = CreateValidData(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(orgId, organizationUser, authenticationData, unlockData));
        Assert.Equal("Organization does not allow password reset.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_OrganizationDoesNotAllowResetPassword_ThrowsBadRequest(
        Organization organization,
        [OrganizationUser] OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        organization.UseResetPassword = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
        var (authenticationData, unlockData) = CreateValidData(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData));
        Assert.Equal("Organization does not allow password reset.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_InvalidPolicy_ThrowsBadRequest(
        Organization organization,
        User user,
        [Policy(PolicyType.ResetPassword, false)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        var (authenticationData, unlockData) = CreateValidData(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, new OrganizationUser { Id = Guid.NewGuid() },
                authenticationData, unlockData));
        Assert.Equal("Organization does not have the password reset policy enabled.", exception.Message);
    }

    public static IEnumerable<object[]> InvalidOrganizationUsers()
    {
        var organization = new Fixture().Create<Organization>();

        var nonConfirmed = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Invited
        };
        yield return [nonConfirmed, organization];

        var wrongOrganization = new OrganizationUser
        {
            Status = OrganizationUserStatusType.Confirmed,
            OrganizationId = Guid.NewGuid(),
            ResetPasswordKey = "test-key",
            UserId = Guid.NewGuid(),
        };
        yield return [wrongOrganization, organization];

        var nullResetPasswordKey = new OrganizationUser
        {
            Status = OrganizationUserStatusType.Confirmed,
            OrganizationId = organization.Id,
            ResetPasswordKey = null,
            UserId = Guid.NewGuid(),
        };
        yield return [nullResetPasswordKey, organization];

        var emptyResetPasswordKey = new OrganizationUser
        {
            Status = OrganizationUserStatusType.Confirmed,
            OrganizationId = organization.Id,
            ResetPasswordKey = "",
            UserId = Guid.NewGuid(),
        };
        yield return [emptyResetPasswordKey, organization];

        var nullUserId = new OrganizationUser
        {
            Status = OrganizationUserStatusType.Confirmed,
            OrganizationId = organization.Id,
            ResetPasswordKey = "test-key",
            UserId = null,
        };
        yield return [nullUserId, organization];
    }

    [Theory]
    [BitMemberAutoData(nameof(InvalidOrganizationUsers))]
    public async Task RecoverAccountAsync_OrganizationUserIsInvalid_ThrowsBadRequest(
        OrganizationUser organizationUser,
        Organization organization,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        var (authenticationData, unlockData) = CreateValidData(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData));
        Assert.Equal("Organization User not valid", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UserDoesNotExist_ThrowsNotFoundException(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(organizationUser.UserId!.Value)
            .Returns((User)null);
        var (authenticationData, unlockData) = CreateValidData(user);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData));
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UserUsesKeyConnector_ThrowsBadRequest(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        user.UsesKeyConnector = true;
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(organizationUser.UserId!.Value)
            .Returns(user);
        var (authenticationData, unlockData) = CreateValidData(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData));
        Assert.Equal("Cannot reset password of a user with Key Connector.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_KdfMismatch_ThrowsBadRequest(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);

        // User has PBKDF2, but submitted data has Argon2id
        user.Kdf = KdfType.PBKDF2_SHA256;
        user.KdfIterations = 600000;
        user.KdfMemory = null;
        user.KdfParallelism = null;
        var mismatchedKdf = new KdfSettings
        {
            KdfType = KdfType.Argon2id,
            Iterations = 3,
            Memory = 64,
            Parallelism = 4
        };
        var salt = user.GetMasterPasswordSalt();
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = mismatchedKdf,
            MasterPasswordAuthenticationHash = "new-master-password-hash",
            Salt = salt
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = mismatchedKdf,
            MasterKeyWrappedUserKey = "encrypted-user-key",
            Salt = salt
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData));
        Assert.Equal("Invalid KDF settings.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UnlockSaltMismatch_ThrowsBadRequest(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);

        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var correctSalt = user.GetMasterPasswordSalt();
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "new-master-password-hash",
            Salt = correctSalt
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "encrypted-user-key",
            Salt = "wrong-salt@example.com"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData));
        Assert.Equal("Invalid master password salt.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_AuthenticationSaltMismatch_ThrowsBadRequest(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);

        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var correctSalt = user.GetMasterPasswordSalt();
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "new-master-password-hash",
            Salt = "wrong-salt@example.com"
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "encrypted-user-key",
            Salt = correctSalt
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData));
        Assert.Equal("Invalid master password salt.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_MapsAuthHashAndKeyCorrectly(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);
        var (authenticationData, unlockData) = CreateValidData(user);
        SetupSuccessfulPasswordUpdate(sutProvider, user, authenticationData.MasterPasswordAuthenticationHash);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, authenticationData, unlockData);

        // Assert
        Assert.True(result.Succeeded);

        // Verify the authentication hash was passed to UpdatePasswordHash
        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdatePasswordHash(user, authenticationData.MasterPasswordAuthenticationHash);

        // Verify the wrapped user key was assigned to user.Key
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u => u.Key == unlockData.MasterKeyWrappedUserKey));
    }

    private static (MasterPasswordAuthenticationData authenticationData, MasterPasswordUnlockData unlockData)
        CreateValidData(User user)
    {
        var kdf = new KdfSettings
        {
            KdfType = user.Kdf,
            Iterations = user.KdfIterations,
            Memory = user.KdfMemory,
            Parallelism = user.KdfParallelism
        };
        var salt = user.GetMasterPasswordSalt();
        var authenticationData = new MasterPasswordAuthenticationData
        {
            Kdf = kdf,
            MasterPasswordAuthenticationHash = "new-master-password-hash",
            Salt = salt
        };
        var unlockData = new MasterPasswordUnlockData
        {
            Kdf = kdf,
            MasterKeyWrappedUserKey = "encrypted-user-key",
            Salt = salt
        };
        return (authenticationData, unlockData);
    }

    private static void SetupValidOrganization(SutProvider<AdminRecoverAccountCommand> sutProvider, Organization organization)
    {
        organization.UseResetPassword = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }

    private static void SetupValidPolicy(SutProvider<AdminRecoverAccountCommand> sutProvider, Organization organization, PolicyStatus policy)
    {
        sutProvider.GetDependency<IPolicyQuery>()
            .RunAsync(organization.Id, PolicyType.ResetPassword)
            .Returns(policy);
    }

    private static void SetupValidOrganizationUser(OrganizationUser organizationUser, Guid orgId)
    {
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = orgId;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.Type = OrganizationUserType.User;
    }

    private static void SetupValidUser(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, OrganizationUser organizationUser)
    {
        user.Id = organizationUser.UserId!.Value;
        user.UsesKeyConnector = false;
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(user.Id)
            .Returns(user);
    }

    private static void SetupSuccessfulPasswordUpdate(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, string newMasterPassword)
    {
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(user, newMasterPassword)
            .Returns(IdentityResult.Success);
    }

    private static async Task AssertSuccessAsync(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, string key,
        Organization organization, OrganizationUser organizationUser)
    {
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u =>
                u.Id == user.Id &&
                u.Key == key &&
                u.ForcePasswordReset == true &&
                u.RevisionDate == u.AccountRevisionDate &&
                u.LastPasswordChangeDate == u.RevisionDate));

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email),
            Arg.Is(user.Name),
            Arg.Is(organization.DisplayName()));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser),
            Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushLogOutAsync(
            Arg.Is(user.Id));
    }
}
