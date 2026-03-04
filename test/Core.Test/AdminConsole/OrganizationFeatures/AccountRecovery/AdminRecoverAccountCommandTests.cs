using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.AccountRecovery;

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

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            newMasterPasswordHash: newMasterPassword, key: key);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdatePasswordHash(user, newMasterPassword);

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

        await sutProvider.GetDependency<IEventService>().DidNotReceive().LogOrganizationUserEventAsync(
            Arg.Any<OrganizationUser>(),
            Arg.Is(EventType.OrganizationUser_AdminResetTwoFactor));

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushLogOutAsync(user.Id);

        await sutProvider.GetDependency<IResetUserTwoFactorCommand>().DidNotReceive()
            .ResetAsync(Arg.Any<User>());
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

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: false, resetTwoFactor: true);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IResetUserTwoFactorCommand>().Received(1)
            .ResetAsync(user);

        await sutProvider.GetDependency<IUserService>().DidNotReceive()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>());

        await sutProvider.GetDependency<IUserRepository>().DidNotReceive()
            .ReplaceAsync(Arg.Any<User>());

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

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: true,
            newMasterPasswordHash: newMasterPassword, key: key);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.True(result.Succeeded);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdatePasswordHash(user, newMasterPassword);

        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u =>
                u.Id == user.Id &&
                u.Key == key &&
                u.ForcePasswordReset == true &&
                u.RevisionDate == u.AccountRevisionDate &&
                u.LastPasswordChangeDate == u.RevisionDate));

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
    public async Task RecoverAccountAsync_UpdatePasswordHashFails_ReturnsFailedIdentityResult(
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

        var failedResult = IdentityResult.Failed(new IdentityError { Description = "Password update failed" });
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(user, newMasterPassword)
            .Returns(failedResult);

        var request = CreateRequest(organization.Id, organizationUser,
            resetMasterPassword: true, resetTwoFactor: false,
            newMasterPasswordHash: newMasterPassword, key: key);

        // Act
        var result = await sutProvider.Sut.RecoverAccountAsync(request);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Description == "Password update failed");

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
}
