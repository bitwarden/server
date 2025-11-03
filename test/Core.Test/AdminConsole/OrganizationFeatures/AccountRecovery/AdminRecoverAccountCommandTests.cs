using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
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
    [Theory]
    [BitAutoData]
    public async Task RecoverAccountAsync_Success(
        string newMasterPassword,
        string key,
        Organization organization,
        OrganizationUser organizationUser,
        User user,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization);
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

    public static IEnumerable<object[]> InvalidPolicies => new object[][]
    {
        [new Policy { Type = PolicyType.ResetPassword, Enabled = false }], [null]
    };

    [Theory]
    [BitMemberAutoData(nameof(InvalidPolicies))]
    public async Task RecoverAccountAsync_InvalidPolicy_ThrowsBadRequest(
        Policy resetPasswordPolicy,
        string newMasterPassword,
        string key,
        Organization organization,
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword)
            .Returns(resetPasswordPolicy);

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
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization);

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
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization);
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
        SutProvider<AdminRecoverAccountCommand> sutProvider)
    {
        // Arrange
        SetupValidOrganization(sutProvider, organization);
        SetupValidPolicy(sutProvider, organization);
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

    private static void SetupValidOrganization(SutProvider<AdminRecoverAccountCommand> sutProvider, Organization organization)
    {
        organization.UseResetPassword = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }

    private static void SetupValidPolicy(SutProvider<AdminRecoverAccountCommand> sutProvider, Organization organization)
    {
        var policy = new Policy { Type = PolicyType.ResetPassword, Enabled = true };
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.ResetPassword)
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
