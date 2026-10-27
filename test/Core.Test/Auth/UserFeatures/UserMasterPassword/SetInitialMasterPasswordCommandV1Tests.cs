using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class SetInitialMasterPasswordCommandV1Tests
{
    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_Success(SutProvider<SetInitialMasterPasswordCommandV1> sutProvider,
        User user, string masterPassword, string key, string orgIdentifier,
        Organization org, OrganizationUser orgUser)
    {
        // Arrange
        user.MasterPassword = null;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgIdentifier)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);

        // Act
        var result = await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgIdentifier);

        // Assert
        Assert.Equal(IdentityResult.Success, result);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_UserIsNull_ThrowsArgumentNullException(SutProvider<SetInitialMasterPasswordCommandV1> sutProvider, string masterPassword, string key, string orgIdentifier)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(null, masterPassword, key, orgIdentifier));
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_AlreadyHasPassword_ReturnsFalse(SutProvider<SetInitialMasterPasswordCommandV1> sutProvider, User user, string masterPassword, string key, string orgIdentifier)
    {
        // Arrange
        user.MasterPassword = "ExistingPassword";

        // Act
        var result = await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgIdentifier);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_NullOrgSsoIdentifier_ThrowsBadRequestException(
        SutProvider<SetInitialMasterPasswordCommandV1> sutProvider, User user, string masterPassword, string key)
    {
        // Arrange
        user.MasterPassword = null;
        string orgSsoIdentifier = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgSsoIdentifier));
        Assert.Equal("Organization SSO Identifier required.", exception.Message);
        await AssertNoCredentialStateWrittenAsync(sutProvider);
    }


    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_InvalidOrganization_Throws(SutProvider<SetInitialMasterPasswordCommandV1> sutProvider, User user, string masterPassword, string key, string orgIdentifier)
    {
        // Arrange
        user.MasterPassword = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgIdentifier)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgIdentifier));
        Assert.Equal("Organization invalid.", exception.Message);
        await AssertNoCredentialStateWrittenAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_UserNotFoundInOrganization_Throws(SutProvider<SetInitialMasterPasswordCommandV1> sutProvider, User user, string masterPassword, string key, Organization org)
    {
        // Arrange
        user.MasterPassword = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(Arg.Any<string>())
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, org.Identifier));
        Assert.Equal("User not found within organization.", exception.Message);
        await AssertNoCredentialStateWrittenAsync(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_ConfirmedOrgUser_DoesNotCallAcceptOrgUser(SutProvider<SetInitialMasterPasswordCommandV1> sutProvider,
        User user, string masterPassword, string key, string orgIdentifier, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        user.MasterPassword = null;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgIdentifier)
            .Returns(org);

        orgUser.Status = OrganizationUserStatusType.Confirmed;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);


        // Act
        var result = await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgIdentifier);

        // Assert
        Assert.Equal(IdentityResult.Success, result);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().DidNotReceive().AcceptOrgUserAsync(Arg.Any<OrganizationUser>(), Arg.Any<User>(), Arg.Any<IUserService>());
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_InvitedOrgUser_CallsAcceptOrgUser(SutProvider<SetInitialMasterPasswordCommandV1> sutProvider,
        User user, string masterPassword, string key, string orgIdentifier, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        user.MasterPassword = null;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgIdentifier)
            .Returns(org);

        orgUser.Status = OrganizationUserStatusType.Invited;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);

        // Act
        var result = await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgIdentifier);

        // Assert
        Assert.Equal(IdentityResult.Success, result);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1).AcceptOrgUserAsync(orgUser, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_PasswordValidationFails_DoesNotPersist(
        SutProvider<SetInitialMasterPasswordCommandV1> sutProvider,
        User user, string masterPassword, string key, string orgIdentifier, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        user.MasterPassword = null;
        var originalKey = user.Key;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgIdentifier)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(IdentityResult.Failed());

        // Act
        var result = await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgIdentifier);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(originalKey, user.Key);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogUserEventAsync(default, default);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().DidNotReceiveWithAnyArgs().AcceptOrgUserAsync(default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_Success_ValidatesOrganizationBeforeWritingCredentials(
        SutProvider<SetInitialMasterPasswordCommandV1> sutProvider,
        User user, string masterPassword, string key, string orgIdentifier, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        user.MasterPassword = null;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(IdentityResult.Success);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgIdentifier)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);

        // Act
        var result = await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgIdentifier);

        // Assert
        Assert.Equal(IdentityResult.Success, result);
        Assert.Equal(key, user.Key);
        Received.InOrder(() =>
        {
            sutProvider.GetDependency<IOrganizationRepository>().GetByIdentifierAsync(orgIdentifier);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(org.Id, user.Id);
            sutProvider.GetDependency<IUserService>().UpdatePasswordHash(user, masterPassword, true, false);
            sutProvider.GetDependency<IUserRepository>().ReplaceAsync(user);
            sutProvider.GetDependency<IEventService>().LogUserEventAsync(user.Id, EventType.User_ChangedPassword);
        });
    }

    private static async Task AssertNoCredentialStateWrittenAsync(SutProvider<SetInitialMasterPasswordCommandV1> sutProvider)
    {
        await sutProvider.GetDependency<IUserService>().DidNotReceiveWithAnyArgs().UpdatePasswordHash(default, default, default, default);
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs().LogUserEventAsync(default, default);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>().DidNotReceiveWithAnyArgs().AcceptOrgUserAsync(default, default, default);
    }
}
