using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
    public async Task AdminResetPasswordAsync_ValidRequest_Success(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        SetupValidOrganizationUser(sutProvider, organizationUser, organization.Id, callingUserType);
        SetupValidUser(sutProvider, user, organizationUser.UserId.Value);
        SetupSuccessfulPasswordUpdate(sutProvider, user, newMasterPassword);

        // Act
        var result = await sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id,
            organizationUser.Id, newMasterPassword, key);

        // Assert
        Assert.True(result.Succeeded);
        await VerifyUserUpdated(sutProvider, user, key);
        await VerifyEmailSent(sutProvider, user, organization);
        await VerifyEventLogged(sutProvider, organizationUser);
        await VerifyPushNotificationSent(sutProvider, user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_OrganizationDoesNotExist_ThrowsBadRequest(
        OrganizationUserType callingUserType,
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
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, orgId, Guid.NewGuid(),
                newMasterPassword, key));
        Assert.Equal("Organization does not allow password reset.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_OrganizationDoesNotAllowResetPassword_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        organization.UseResetPassword = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, Guid.NewGuid(),
                newMasterPassword, key));
        Assert.Equal("Organization does not allow password reset.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_PolicyDoesNotExist_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword)
            .Returns((Policy)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, Guid.NewGuid(),
                newMasterPassword, key));
        Assert.Equal("Organization does not have the password reset policy enabled.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_PolicyNotEnabled_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        resetPasswordPolicy.Enabled = false;
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword)
            .Returns(resetPasswordPolicy);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, Guid.NewGuid(),
                newMasterPassword, key));
        Assert.Equal("Organization does not have the password reset policy enabled.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_OrganizationUserDoesNotExist_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        var orgUserId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(orgUserId)
            .Returns((OrganizationUser)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, orgUserId, newMasterPassword, key));
        Assert.Equal("Organization User not valid", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_OrganizationUserNotConfirmed_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        organizationUser.Status = OrganizationUserStatusType.Invited;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key));
        Assert.Equal("Organization User not valid", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_OrganizationUserWrongOrganization_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = Guid.NewGuid(); // Different org
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key));
        Assert.Equal("Organization User not valid", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_OrganizationUserNoResetPasswordKey_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.ResetPasswordKey = null;
        organizationUser.UserId = Guid.NewGuid();
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key));
        Assert.Equal("Organization User not valid", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_OrganizationUserNoUserId_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = null;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key));
        Assert.Equal("Organization User not valid", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin, OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Custom, OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Custom, OrganizationUserType.Admin)]
    public async Task AdminResetPasswordAsync_InsufficientPermissions_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        OrganizationUserType targetUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = Guid.NewGuid();
        organizationUser.Type = targetUserType;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key));
        Assert.Equal("Calling user does not have permission to reset this user's master password", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_UserDoesNotExist_ThrowsNotFoundException(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        SetupValidOrganizationUser(sutProvider, organizationUser, organization.Id, callingUserType);
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(organizationUser.UserId.Value)
            .Returns((User)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key));
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_UserUsesKeyConnector_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        SetupValidOrganizationUser(sutProvider, organizationUser, organization.Id, callingUserType);
        user.UsesKeyConnector = true;
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(organizationUser.UserId.Value)
            .Returns(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key));
        Assert.Equal("Cannot reset password of a user with Key Connector.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_PasswordUpdateFails_ReturnsFailure(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        SetupValidOrganizationUser(sutProvider, organizationUser, organization.Id, callingUserType);
        SetupValidUser(sutProvider, user, organizationUser.UserId.Value);

        var failedResult = IdentityResult.Failed(new IdentityError { Description = "Password update failed" });
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(user, newMasterPassword)
            .Returns(failedResult);

        // Act
        var result = await sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Description == "Password update failed");
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner, OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Owner, OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner, OrganizationUserType.Custom)]
    [BitAutoData(OrganizationUserType.Owner, OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Admin, OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Admin, OrganizationUserType.Custom)]
    [BitAutoData(OrganizationUserType.Admin, OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom, OrganizationUserType.Custom)]
    [BitAutoData(OrganizationUserType.Custom, OrganizationUserType.User)]
    public async Task AdminResetPasswordAsync_ValidPermissionCombinations_Success(
        OrganizationUserType callingUserType,
        OrganizationUserType targetUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = organization.Id;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = Guid.NewGuid();
        organizationUser.Type = targetUserType;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
        SetupValidUser(sutProvider, user, organizationUser.UserId.Value);
        SetupSuccessfulPasswordUpdate(sutProvider, user, newMasterPassword);

        // Act
        var result = await sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    public async Task AdminResetPasswordAsync_CallingUserNotProviderForTargetUser_ThrowsBadRequest(
        OrganizationUserType callingUserType,
        string newMasterPassword,
        string key,
        Organization organization,
        Policy resetPasswordPolicy,
        OrganizationUser organizationUser,
        User user,
        ProviderUser providerUser,
        ProviderOrganization providerOrganization,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, resetPasswordPolicy, organization.Id);
        SetupValidOrganizationUser(sutProvider, organizationUser, organization.Id, callingUserType);
        SetupValidUser(sutProvider, user, organizationUser.UserId.Value);
        SetupSuccessfulPasswordUpdate(sutProvider, user, newMasterPassword);

        // Setup provider relationship - the target user is a provider for the organization
        providerUser.UserId = organizationUser.UserId.Value;
        providerUser.ProviderId = providerOrganization.ProviderId;
        providerUser.Status = ProviderUserStatusType.Confirmed;
        providerOrganization.OrganizationId = organization.Id;

        // Mock the provider organization repository to return the provider organization
        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetByOrganizationId(organization.Id)
            .Returns(providerOrganization);

        // Mock the current context to return a calling user ID
        var callingUserId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(callingUserId);

        // Mock the provider user repository to return the provider user for the target user
        // and no provider user for the calling user
        var providerUsers = new List<ProviderUser> { providerUser };
        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByProviderAsync(providerOrganization.ProviderId)
            .Returns(providerUsers);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdminResetPasswordAsync(callingUserType, organization.Id, organizationUser.Id, newMasterPassword, key));
        Assert.Equal("Calling user does not have permission to reset this user's master password", exception.Message);
    }

    private void SetupValidOrganization(SutProvider<AdminRecoverAccountCommand> sutProvider, Organization organization)
    {
        organization.UseResetPassword = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }

    private void SetupValidPolicy(SutProvider<AdminRecoverAccountCommand> sutProvider, Policy resetPasswordPolicy, Guid orgId)
    {
        resetPasswordPolicy.Enabled = true;
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(orgId, PolicyType.ResetPassword)
            .Returns(resetPasswordPolicy);
    }

    private void SetupValidOrganizationUser(SutProvider<AdminRecoverAccountCommand> sutProvider, OrganizationUser organizationUser, Guid orgId, OrganizationUserType callingUserType)
    {
        organizationUser.Status = OrganizationUserStatusType.Confirmed;
        organizationUser.OrganizationId = orgId;
        organizationUser.ResetPasswordKey = "test-key";
        organizationUser.UserId = Guid.NewGuid();
        // Set a user type that the calling user can reset
        organizationUser.Type = callingUserType == OrganizationUserType.Owner ? OrganizationUserType.Admin : OrganizationUserType.User;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
    }

    private void SetupValidUser(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, Guid userId)
    {
        user.UsesKeyConnector = false;
        sutProvider.GetDependency<IUserService>()
            .GetUserByIdAsync(userId)
            .Returns(user);
    }

    private void SetupSuccessfulPasswordUpdate(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, string newMasterPassword)
    {
        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(user, newMasterPassword)
            .Returns(IdentityResult.Success);
    }

    private async Task VerifyUserUpdated(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, string key)
    {
        await sutProvider.GetDependency<IUserRepository>().Received(1).ReplaceAsync(
            Arg.Is<User>(u =>
                u.Id == user.Id &&
                u.Key == key &&
                u.ForcePasswordReset == true &&
                u.RevisionDate == u.AccountRevisionDate &&
                u.LastPasswordChangeDate == u.RevisionDate));
    }

    private async Task VerifyEmailSent(SutProvider<AdminRecoverAccountCommand> sutProvider, User user, Organization organization)
    {
        await sutProvider.GetDependency<IMailService>().Received(1).SendAdminResetPasswordEmailAsync(
            Arg.Is(user.Email),
            Arg.Is(user.Name),
            Arg.Is(organization.DisplayName()));
    }

    private async Task VerifyEventLogged(SutProvider<AdminRecoverAccountCommand> sutProvider, OrganizationUser organizationUser)
    {
        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(
            Arg.Is(organizationUser),
            Arg.Is(EventType.OrganizationUser_AdminResetPassword));
    }

    private async Task VerifyPushNotificationSent(SutProvider<AdminRecoverAccountCommand> sutProvider, Guid userId)
    {
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushLogOutAsync(
            Arg.Is(userId));
    }
}
