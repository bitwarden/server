using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
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
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.AccountRecovery;

[SutProviderCustomize]
public class AdminRecoverAccountCommandTests
{
    // -----------------------------------------------------------------------
    // New Master Password Hash and Key provided.
    //
    // These cover the pre-migration guards (org, policy, user-state, KC).
    // They remain valid after the migration because the guards execute before
    // the service call and are not affected by the signature change.
    // -----------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_Success(
        string newMasterPassword,
        string key,
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
        SetupSuccessfulPasswordUpdate(sutProvider, user, newMasterPassword);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, newMasterPassword, key);

        // Assert
        Assert.True(result.Succeeded);
        await AssertSuccessAsync(sutProvider, user, key, organization, organizationUser);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_OrganizationDoesNotExist_ThrowsBadRequest(
        [OrganizationUser] OrganizationUser organizationUser,
        string newMasterPassword,
        string key,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(orgId)
            .Returns((Organization)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(orgId, organizationUser, newMasterPassword, key));
        Assert.Equal("Organization does not allow password reset.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_OrganizationDoesNotAllowResetPassword_ThrowsBadRequest(
        string newMasterPassword,
        string key,
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
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, newMasterPassword, key));
        Assert.Equal("Organization does not allow password reset.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_InvalidPolicy_ThrowsBadRequest(
        string newMasterPassword,
        string key,
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
                newMasterPassword, key));
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
        string newMasterPassword,
        string key,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, newMasterPassword, key));
        Assert.Equal("Organization User not valid", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UserDoesNotExist_ThrowsNotFoundException(
        string newMasterPassword,
        string key,
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
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, newMasterPassword, key));
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UserUsesKeyConnector_ThrowsBadRequest(
        string newMasterPassword,
        string key,
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
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, newMasterPassword, key));
        Assert.Equal("Cannot reset password of a user with Key Connector.", exception.Message);
    }

    // -----------------------------------------------------------------------
    // AuthenticationData and UnlockData provided.
    //
    // PrepareSetInitialOrUpdateExistingMasterPasswordAsync routes set-initial
    // vs update-existing internally on user.HasMasterPassword(). These tests
    // pin that v1 issues a single service call regardless of the target user's
    // master-password state.
    // -----------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithNewPayload_ServiceSucceeds_SetsForcedReset_PersistsAndInvokesSideEffects(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);
        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(user, Arg.Any<Bit.Core.Auth.UserFeatures.UserMasterPassword.Data.SetInitialOrUpdateExistingPasswordData>())
            .Returns(user);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(
            organization.Id, organizationUser, unlockData, authenticationData);

        // Assert — return type
        Assert.True(result.Succeeded);

        // Assert — ForcePasswordReset set before persist
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u => u.ForcePasswordReset == true));

        // Assert — side effects fired
        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email),
            Arg.Is(user.Name),
            Arg.Is(organization.DisplayName()),
            Arg.Is(true),
            Arg.Is(false));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser),
            Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(Arg.Is(user.Id));
    }

    // Atomicity: any failure (service-level, upstream, or framework-edge) leaves
    // the system unchanged — no persisted user mutation, no email, no event log,
    // no logout push. Recovery is all-or-nothing.
    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithNewPayload_FailureIsAtomic_NoPersistOrInvokeSideEffects(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        IdentityError[] identityErrors,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        SetupValidUser(sutProvider, user, organizationUser);
        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(user, Arg.Any<Bit.Core.Auth.UserFeatures.UserMasterPassword.Data.SetInitialOrUpdateExistingPasswordData>())
            .Returns(identityErrors);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(
            organization.Id, organizationUser, unlockData, authenticationData);

        // Assert — failure propagated
        Assert.False(result.Succeeded);
        Assert.Equal(identityErrors.Length, result.Errors.Count());

        // Assert — no persist on failure
        await sutProvider.GetDependency<IUserRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<User>());

        // Assert — no side effects on failure
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendAdminResetPasswordEmailAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<bool>());

        await sutProvider.GetDependency<IEventService>().DidNotReceive()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>());

        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushLogOutAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithNewPayload_SingleServiceCallRegardlessOfUserMasterPasswordState_UserHasPassword(
        Organization organization,
        OrganizationUser organizationUser,
        User userWithExistingPassword,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange — user has an existing master password
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        userWithExistingPassword.Id = organizationUser.UserId!.Value;
        userWithExistingPassword.UsesKeyConnector = false;
        userWithExistingPassword.MasterPassword = "existing-hash"; // has a password
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(userWithExistingPassword.Id)
            .Returns(userWithExistingPassword);
        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                userWithExistingPassword,
                Arg.Any<Bit.Core.Auth.UserFeatures.UserMasterPassword.Data.SetInitialOrUpdateExistingPasswordData>())
            .Returns(userWithExistingPassword);

        // Act
        await sutProvider.Sut.RecoverAccountAsync(
            organization.Id, organizationUser, unlockData, authenticationData);

        // Assert — unified dispatch: exactly one call to PrepareSetInitialOrUpdateExisting,
        // NOT separate calls to PrepareUpdateExisting or PrepareSetInitial
        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                Arg.Is(userWithExistingPassword),
                Arg.Any<Bit.Core.Auth.UserFeatures.UserMasterPassword.Data.SetInitialOrUpdateExistingPasswordData>());
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithNewPayload_SingleServiceCallRegardlessOfUserMasterPasswordState_UserHasNoPassword(
        Organization organization,
        OrganizationUser organizationUser,
        User userWithoutPassword,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange — user has no existing master password (e.g., TDE user)
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        userWithoutPassword.Id = organizationUser.UserId!.Value;
        userWithoutPassword.UsesKeyConnector = false;
        userWithoutPassword.MasterPassword = null; // no password
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(userWithoutPassword.Id)
            .Returns(userWithoutPassword);
        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                userWithoutPassword,
                Arg.Any<Bit.Core.Auth.UserFeatures.UserMasterPassword.Data.SetInitialOrUpdateExistingPasswordData>())
            .Returns(userWithoutPassword);

        // Act
        await sutProvider.Sut.RecoverAccountAsync(
            organization.Id, organizationUser, unlockData, authenticationData);

        // Assert — unified dispatch: exactly one call regardless of user state
        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                Arg.Is(userWithoutPassword),
                Arg.Any<Bit.Core.Auth.UserFeatures.UserMasterPassword.Data.SetInitialOrUpdateExistingPasswordData>());
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithNewPayload_UserUsesKeyConnector_ThrowsBadRequest(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        [Policy(PolicyType.ResetPassword, true)] PolicyStatus policy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange — KC guard applies regardless of payload type (defense in depth)
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization, policy);
        SetupValidOrganizationUser(organizationUser, organization.Id);
        user.Id = organizationUser.UserId!.Value;
        user.UsesKeyConnector = true;
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(user.Id)
            .Returns(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RecoverAccountAsync(organization.Id, organizationUser, unlockData, authenticationData));
        Assert.Equal("Cannot reset password of a user with Key Connector.", exception.Message);

        // Assert — service never reached
        await sutProvider.GetDependency<IMasterPasswordService>().DidNotReceive()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(Arg.Any<User>(),
                Arg.Any<Bit.Core.Auth.UserFeatures.UserMasterPassword.Data.SetInitialOrUpdateExistingPasswordData>());
    }

    // -----------------------------------------------------------------------
    // Helpers — shared between old and new tests
    // -----------------------------------------------------------------------

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
