using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    public async Task RecoverAccountAsync_ResetMasterPasswordOnly_Success(
        string newMasterPassword,
        string key,
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupSuccessfulPasswordUpdate(sutProvider, user, newMasterPassword);
        SetupPolicy(sutProvider, user);

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            newMasterPasswordHash: newMasterPassword, key: key);
        SetupValidValidator(sutProvider);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdatePasswordHash(user, newMasterPassword);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);

        Assert.Equal(key, user.Key);
        Assert.True(user.ForcePasswordReset);
        Assert.Equal(user.RevisionDate, user.AccountRevisionDate);
        Assert.Equal(user.RevisionDate, user.LastPasswordChangeDate);

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email),
            Arg.Is(user.Name),
            Arg.Is(organization.DisplayName()),
            Arg.Is(true),
            Arg.Is(false));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser),
            Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IEventService>().DidNotReceive().LogOrganizationUserEventAsync(
            Arg.Any<OrganizationUser>(),
            Arg.Is(EventType.OrganizationUser_AdminResetTwoFactor));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_ResetTwoFactorOnly_Success(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupPolicy(sutProvider, user);

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: false, resetTwoFactor: true);
        SetupValidValidator(sutProvider);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IResetUserTwoFactorCommand>().Received(1)
            .ResetAsync(user);

        await sutProvider.GetDependency<IUserService>().DidNotReceive()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>());

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email),
            Arg.Is(user.Name),
            Arg.Is(organization.DisplayName()),
            Arg.Is(false),
            Arg.Is(true));

        await sutProvider.GetDependency<IEventService>().DidNotReceive().LogOrganizationUserEventAsync(
            Arg.Any<OrganizationUser>(),
            Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser),
            Arg.Is(EventType.OrganizationUser_AdminResetTwoFactor));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_ResetBoth_Success(
        string newMasterPassword,
        string key,
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupSuccessfulPasswordUpdate(sutProvider, user, newMasterPassword);
        SetupPolicy(sutProvider, user);

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: true,
            newMasterPasswordHash: newMasterPassword, key: key);
        SetupValidValidator(sutProvider);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdatePasswordHash(user, newMasterPassword);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);

        Assert.Equal(key, user.Key);
        Assert.True(user.ForcePasswordReset);
        Assert.Equal(user.RevisionDate, user.AccountRevisionDate);
        Assert.Equal(user.RevisionDate, user.LastPasswordChangeDate);

        await sutProvider.GetDependency<IResetUserTwoFactorCommand>().Received(1)
            .ResetAsync(user);

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email),
            Arg.Is(user.Name),
            Arg.Is(organization.DisplayName()),
            Arg.Is(true),
            Arg.Is(true));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser),
            Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser),
            Arg.Is(EventType.OrganizationUser_AdminResetTwoFactor));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_UpdatePasswordHashFails_ReturnsError(
        string newMasterPassword,
        string key,
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupPolicy(sutProvider, user);

        var failedResult = IdentityResult.Failed(new IdentityError { Description = "Password update failed" });
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(user, newMasterPassword)
            .Returns(failedResult);

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            newMasterPasswordHash: newMasterPassword, key: key);
        SetupValidValidator(sutProvider);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<PasswordUpdateFailedError>(result.AsError);
        Assert.Contains("Password update failed", result.AsError.Message);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<User>());

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
    public async Task RecoverAccountAsync_ValidationFails_ReturnsError(
        Organization organization,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: false, resetTwoFactor: false);

        sutProvider.GetDependency<IAdminRecoverAccountValidator>()
            .ValidateAsync(Arg.Any<RecoverAccountRequest>())
            .Returns(callInfo => ValidationResultHelpers.Invalid(
                callInfo.Arg<RecoverAccountRequest>(), new NoActionRequestedError()));

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<NoActionRequestedError>(result.AsError);

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<User>());
    }

    // -----------------------------------------------------------------------
    // AuthenticationData + UnlockData provided.
    //
    // When the request carries AuthenticationData + UnlockData, the command
    // MUST delegate to PrepareSetInitialOrUpdateExistingMasterPasswordAsync
    // rather than the UpdatePasswordHash path. These tests cover that dispatch
    // and its OneOf success/failure handling, across the MP-only and combined
    // reset variants.
    // -----------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithUnlockAndAuthenticationData_ResetMasterPasswordOnly_ServiceSucceeds_SetsForcedReset_PersistsAndInvokesSideEffects(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupPolicy(sutProvider, user);
        SetupValidValidator(sutProvider);

        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>())
            .Returns(user);

        var request = CreateUnlockAndAuthenticationDataRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            unlockData: unlockData, authenticationData: authenticationData);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>());

        await sutProvider.GetDependency<IUserService>().DidNotReceive()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>());

        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.ForcePasswordReset == true));

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email), Arg.Is(user.Name), Arg.Is(organization.DisplayName()),
            Arg.Is(true), Arg.Is(false));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser), Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IEventService>().DidNotReceive().LogOrganizationUserEventAsync(
            Arg.Any<OrganizationUser>(), Arg.Is(EventType.OrganizationUser_AdminResetTwoFactor));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    // Atomicity: a service failure must leave the system unchanged — no persist, no email,
    // no event, no logout push. Recovery is all-or-nothing.
    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithUnlockAndAuthenticationData_ResetMasterPasswordOnly_FailureIsAtomic_NoPersistOrInvokeSideEffects(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        IdentityError[] identityErrors,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupPolicy(sutProvider, user);
        SetupValidValidator(sutProvider);

        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>())
            .Returns(identityErrors);

        var request = CreateUnlockAndAuthenticationDataRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            unlockData: unlockData, authenticationData: authenticationData);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert — failure propagated
        Assert.True(result.IsError);
        Assert.IsType<PasswordUpdateFailedError>(result.AsError);

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
    public async Task RecoverAccountAsync_WithUnlockAndAuthenticationData_ResetBoth_Success(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupPolicy(sutProvider, user);
        SetupValidValidator(sutProvider);

        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>())
            .Returns(user);

        var request = CreateUnlockAndAuthenticationDataRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: true,
            unlockData: unlockData, authenticationData: authenticationData);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>());

        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.ForcePasswordReset == true));

        await sutProvider.GetDependency<IResetUserTwoFactorCommand>().Received(1)
            .ResetAsync(user);

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email), Arg.Is(user.Name), Arg.Is(organization.DisplayName()),
            Arg.Is(true), Arg.Is(true));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser), Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser), Arg.Is(EventType.OrganizationUser_AdminResetTwoFactor));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    // PrepareSetInitialOrUpdateExistingMasterPasswordAsync routes set-initial vs
    // update-existing internally based on user.HasMasterPassword(). These two tests
    // pin that v2 issues a single service call regardless of the target user's
    // master-password state — dispatch consolidation is not observable from outside the service.
    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithUnlockAndAuthenticationData_SingleServiceCallRegardlessOfUserMasterPasswordState_UserHasPassword(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange — user has an existing master password
        SetupOrganization(sutProvider, organization);
        SetupPolicy(sutProvider, user);
        SetupValidValidator(sutProvider);
        user.Id = organizationUser.UserId!.Value;
        user.MasterPassword = "existing-hash";
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>())
            .Returns(user);

        var request = CreateUnlockAndAuthenticationDataRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            unlockData: unlockData, authenticationData: authenticationData);

        // Act
        await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert — unified dispatch: one call to PrepareSetInitialOrUpdateExisting regardless of user state
        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>());
    }

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithUnlockAndAuthenticationData_SingleServiceCallRegardlessOfUserMasterPasswordState_UserHasNoPassword(
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange — user has no existing master password (e.g., TDE user)
        SetupOrganization(sutProvider, organization);
        SetupPolicy(sutProvider, user);
        SetupValidValidator(sutProvider);
        user.Id = organizationUser.UserId!.Value;
        user.MasterPassword = null;
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);
        sutProvider.GetDependency<IMasterPasswordService>()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>())
            .Returns(user);

        var request = CreateUnlockAndAuthenticationDataRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            unlockData: unlockData, authenticationData: authenticationData);

        // Act
        await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert — unified dispatch: one call regardless of user state
        await sutProvider.GetDependency<IMasterPasswordService>().Received(1)
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                user, Arg.Any<SetInitialOrUpdateExistingPasswordData>());
    }

    // -----------------------------------------------------------------------
    // NewMasterPasswordHash + Key provided.
    //
    // When the request carries NewMasterPasswordHash + Key, the command takes
    // the UpdatePasswordHash path. These tests cover dispatch and its
    // success/failure handling across the MP-only and combined reset variants.
    // -----------------------------------------------------------------------

    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithHashAndKey_ResetMasterPasswordOnly_Success(
        string newMasterPassword,
        string key,
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupSuccessfulPasswordUpdate(sutProvider, user, newMasterPassword);
        SetupPolicy(sutProvider, user);
        SetupValidValidator(sutProvider);

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            newMasterPasswordHash: newMasterPassword, key: key);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdatePasswordHash(user, newMasterPassword);

        await sutProvider.GetDependency<IMasterPasswordService>().DidNotReceive()
            .PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
                Arg.Any<User>(), Arg.Any<SetInitialOrUpdateExistingPasswordData>());

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);

        Assert.Equal(key, user.Key);
        Assert.True(user.ForcePasswordReset);

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email), Arg.Is(user.Name), Arg.Is(organization.DisplayName()),
            Arg.Is(true), Arg.Is(false));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser), Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    // Atomicity: a legacy-path failure must also leave the system unchanged.
    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_WithHashAndKey_ResetMasterPasswordOnly_FailureIsAtomic_NoPersistOrInvokeSideEffects(
        string newMasterPassword,
        string key,
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupPolicy(sutProvider, user);
        SetupValidValidator(sutProvider);

        var failedResult = IdentityResult.Failed(new IdentityError { Description = "Password update failed" });
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(user, newMasterPassword)
            .Returns(failedResult);

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            newMasterPasswordHash: newMasterPassword, key: key);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert — failure propagated
        Assert.True(result.IsError);
        Assert.IsType<PasswordUpdateFailedError>(result.AsError);

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
    public async Task RecoverAccountAsync_WithHashAndKey_ResetBoth_Success(
        string newMasterPassword,
        string key,
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupOrganization(sutProvider, organization);
        SetupUser(sutProvider, user, organizationUser);
        SetupSuccessfulPasswordUpdate(sutProvider, user, newMasterPassword);
        SetupPolicy(sutProvider, user);
        SetupValidValidator(sutProvider);

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: true,
            newMasterPasswordHash: newMasterPassword, key: key);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdatePasswordHash(user, newMasterPassword);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(user);

        await sutProvider.GetDependency<IResetUserTwoFactorCommand>().Received(1)
            .ResetAsync(user);

        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email), Arg.Is(user.Name), Arg.Is(organization.DisplayName()),
            Arg.Is(true), Arg.Is(true));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser), Arg.Is(EventType.OrganizationUser_AdminResetPassword));

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser), Arg.Is(EventType.OrganizationUser_AdminResetTwoFactor));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);
    }

    private static RecoverAccountRequest CreateRequest(
        Guid orgId,
        OrganizationUser organizationUser,
        bool resetMasterPassword,
        bool resetTwoFactor,
        string? newMasterPasswordHash = null,
        string? key = null)
    {
        return new RecoverAccountRequest
        {
            OrgId = orgId,
            OrganizationUser = organizationUser,
            ResetMasterPassword = resetMasterPassword,
            ResetTwoFactor = resetTwoFactor,
            NewMasterPasswordHash = newMasterPasswordHash,
            Key = key,
        };
    }

    private static RecoverAccountRequest CreateUnlockAndAuthenticationDataRequest(
        Guid orgId,
        OrganizationUser organizationUser,
        bool resetMasterPassword,
        bool resetTwoFactor,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData)
    {
        return new RecoverAccountRequest
        {
            OrgId = orgId,
            OrganizationUser = organizationUser,
            ResetMasterPassword = resetMasterPassword,
            ResetTwoFactor = resetTwoFactor,
            UnlockData = unlockData,
            AuthenticationData = authenticationData,
        };
    }

    private static void SetupValidValidator(SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        sutProvider.GetDependency<IAdminRecoverAccountValidator>()
            .ValidateAsync(Arg.Any<RecoverAccountRequest>())
            .Returns(callInfo => ValidationResultHelpers.Valid(callInfo.Arg<RecoverAccountRequest>()));
    }

    private static void SetupOrganization(SutProvider<AdminRecoverAccountCommand> sutProvider, Organization organization)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }

    private static void SetupUser(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, OrganizationUser organizationUser)
    {
        user.Id = organizationUser.UserId!.Value;
        sutProvider.GetDependency<IUserRepository>()
            .GetByIdAsync(user.Id)
            .Returns(user);
    }

    private static void SetupSuccessfulPasswordUpdate(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, string newMasterPassword)
    {
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(user, newMasterPassword)
            .Returns(IdentityResult.Success);
    }

    private static void SetupPolicy(SutProvider<AdminRecoverAccountCommand> sutProvider, User user)
    {
        // 2FA policy does not apply
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement([]));
    }
}
