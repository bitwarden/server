using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
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
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.AccountRecovery;

[SutProviderCustomize]
public class AdminRecoverAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UserHasMasterPassword_CallsUpdate(
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
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
        SetupValidUser(sutProvider, user, organizationUser, hasMasterPassword: true);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, unlockData, authenticationData);

        // Assert
        Assert.True(result.Succeeded);
        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .OnlyMutateUserUpdateExistingMasterPasswordAsync(
                Arg.Any<User>(),
                Arg.Is<UpdateExistingPasswordData>(d =>
                    d.MasterPasswordUnlock == unlockData &&
                    d.MasterPasswordAuthentication == authenticationData));
        await sutProvider.GetDependency<IMasterPasswordService>().DidNotReceive()
            .OnlyMutateUserSetInitialMasterPasswordAsync(Arg.Any<User>(), Arg.Any<SetInitialPasswordData>());
        await AssertCommonSuccessSideEffectsAsync(sutProvider, user, organization, organizationUser);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UserHasNoMasterPassword_CallsSetInitial(
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
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
        SetupValidUser(sutProvider, user, organizationUser, hasMasterPassword: false);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, unlockData, authenticationData);

        // Assert
        Assert.True(result.Succeeded);
        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .OnlyMutateUserSetInitialMasterPasswordAsync(
                Arg.Any<User>(),
                Arg.Is<SetInitialPasswordData>(d =>
                    d.MasterPasswordUnlock == unlockData &&
                    d.MasterPasswordAuthentication == authenticationData));
        await sutProvider.GetDependency<IMasterPasswordService>().DidNotReceive()
            .OnlyMutateUserUpdateExistingMasterPasswordAsync(Arg.Any<User>(), Arg.Any<UpdateExistingPasswordData>());
        await AssertCommonSuccessSideEffectsAsync(sutProvider, user, organization, organizationUser);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_OrganizationDoesNotExist_ThrowsBadRequest(
        [OrganizationUser] OrganizationUser organizationUser,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns((Organization)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(orgId, organizationUser, unlockData, authenticationData));
        Assert.Equal("Organization does not allow password reset.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_OrganizationDoesNotAllowResetPassword_ThrowsBadRequest(
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        Organization organization,
        [OrganizationUser] OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        organization.UseResetPassword = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, unlockData, authenticationData));
        Assert.Equal("Organization does not allow password reset.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_InvalidPolicy_ThrowsBadRequest(
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        Organization organization,
        [Policy(PolicyType.ResetPassword, false)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, new OrganizationUser { Id = Guid.NewGuid() },
                unlockData, authenticationData));
        Assert.Equal("Organization does not have the password reset policy enabled.", exception.Message);
    }

    public static IEnumerable<object[]> InvalidOrganizationUsers()
    {
        // Make an organization so we can use its Id
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
            OrganizationId = Guid.NewGuid(), // Different org
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

        var whitespaceResetPasswordKey = new OrganizationUser
        {
            Status = OrganizationUserStatusType.Confirmed,
            OrganizationId = organization.Id,
            ResetPasswordKey = " ",
            UserId = Guid.NewGuid(),
        };
        yield return [whitespaceResetPasswordKey, organization];

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
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, unlockData, authenticationData));
        Assert.Equal("Organization User not valid", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UserDoesNotExist_ThrowsNotFoundException(
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        Organization organization,
        OrganizationUser organizationUser,
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

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, unlockData, authenticationData));
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UserUsesKeyConnector_ThrowsBadRequest(
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
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

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, unlockData, authenticationData));
        Assert.Equal("Cannot reset password of a user with Key Connector.", exception.Message);
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

    private static void SetupValidUser(SutProvider<AdminRecoverAccountCommand> sutProvider, User user,
        OrganizationUser organizationUser, bool hasMasterPassword)
    {
        user.Id = organizationUser.UserId!.Value;
        user.UsesKeyConnector = false;
        user.MasterPassword = hasMasterPassword ? "existing-hash" : null;
        user.Key = hasMasterPassword ? user.Key : null;
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IMasterPasswordService>()
            .OnlyMutateUserUpdateExistingMasterPasswordAsync(Arg.Any<User>(), Arg.Any<UpdateExistingPasswordData>())
            .Returns(Microsoft.AspNetCore.Identity.IdentityResult.Success);
        sutProvider.GetDependency<IMasterPasswordService>()
            .OnlyMutateUserSetInitialMasterPasswordAsync(Arg.Any<User>(), Arg.Any<SetInitialPasswordData>())
            .Returns(Microsoft.AspNetCore.Identity.IdentityResult.Success);
    }

    private static async Task AssertCommonSuccessSideEffectsAsync(SutProvider<AdminRecoverAccountCommand> sutProvider,
        User user, Organization organization, OrganizationUser organizationUser)
    {
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u => u.Id == user.Id && u.ForcePasswordReset));

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email),
            Arg.Is(user.Name),
            Arg.Is(organization.DisplayName()),
            Arg.Is(true),
            Arg.Is(false));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser),
            Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushLogOutAsync(
            Arg.Is(user.Id));
    }
}
