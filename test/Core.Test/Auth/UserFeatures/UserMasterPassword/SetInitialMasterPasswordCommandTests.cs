using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.UserMasterPassword;

[SutProviderCustomize]
public class SetInitialMasterPasswordCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_Success(SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, string masterPassword, string key, string orgIdentifier,
        Organization org, OrganizationUser orgUser)
    {
        // Arrange
        user.MasterPassword = null;
        var identityResult = IdentityResult.Success;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(identityResult);

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
    public async Task SetInitialMasterPassword_UserIsNull_ThrowsArgumentNullException(SutProvider<SetInitialMasterPasswordCommand> sutProvider, string masterPassword, string key, string orgIdentifier)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(null, masterPassword, key, orgIdentifier));
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_AlreadyHasPassword(SutProvider<SetInitialMasterPasswordCommand> sutProvider, User user, string masterPassword, string key, string orgIdentifier)
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
    public async Task SetInitialMasterPassword_InvalidOrganization_Throws(SutProvider<SetInitialMasterPasswordCommand> sutProvider, User user, string masterPassword, string key, string orgIdentifier)
    {
        // Arrange
        user.MasterPassword = null;
        var identityResult = IdentityResult.Success;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(identityResult);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgIdentifier)
            .ReturnsNull();

        // Act & Assert
       var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, orgIdentifier));
       Assert.Equal("Organization invalid.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_UserNotFoundInOrganization_Throws(SutProvider<SetInitialMasterPasswordCommand> sutProvider, User user, string masterPassword, string key, Organization org)
    {
        // Arrange
        user.MasterPassword = null;
        var identityResult = IdentityResult.Success;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(identityResult);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(Arg.Any<string>())
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, masterPassword, key, org.Identifier));
        Assert.Equal("User not found within organization.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_ConfirmedOrgUser_DoesNotCallAcceptOrgUser(SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, string masterPassword, string key, string orgIdentifier, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        user.MasterPassword = null;
        var identityResult = IdentityResult.Success;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(identityResult);

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
    public async Task SetInitialMasterPassword_InvitedOrgUser_CallsAcceptOrgUser(SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, string masterPassword, string key, string orgIdentifier, Organization org, OrganizationUser orgUser)
    {
        // Arrange
        user.MasterPassword = null;
        var identityResult = IdentityResult.Success;

        sutProvider.GetDependency<IUserService>()
            .UpdatePasswordHash(Arg.Any<User>(), Arg.Any<string>(), true, false)
            .Returns(identityResult);

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

}
