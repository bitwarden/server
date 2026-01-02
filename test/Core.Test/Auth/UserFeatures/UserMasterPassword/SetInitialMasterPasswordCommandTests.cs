using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
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
public class SetInitialMasterPasswordCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_Success(SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, UserAccountKeysData accountKeys, KdfSettings kdfSettings,
        Organization org, OrganizationUser orgUser, string serverSideHash, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        var model = CreateValidModel(user, accountKeys, kdfSettings, org.Identifier, masterPasswordHint);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(org.Identifier)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);

        sutProvider.GetDependency<IPasswordHasher<User>>()
            .HashPassword(user, model.MasterPasswordAuthentication.MasterPasswordAuthenticationHash)
            .Returns(serverSideHash);

        // Mock SetMasterPassword to return a specific UpdateUserData delegate
        UpdateUserData mockUpdateUserData = (connection, transaction) => Task.CompletedTask;
        sutProvider.GetDependency<IUserRepository>()
            .SetMasterPassword(user.Id, model.MasterPasswordUnlock, serverSideHash, model.MasterPasswordHint)
            .Returns(mockUpdateUserData);

        // Act
        await sutProvider.Sut.SetInitialMasterPasswordAsync(user, model);

        // Assert
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .SetV2AccountCryptographicStateAsync(
                user.Id,
                model.AccountKeys,
                Arg.Do<IEnumerable<UpdateUserData>>(actions =>
                {
                    var actionsList = actions.ToList();
                    Assert.Single(actionsList);
                    Assert.Same(mockUpdateUserData, actionsList[0]);
                }));

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_ChangedPassword);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1)
            .AcceptOrgUserAsync(orgUser, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_UserAlreadyHasPassword_ThrowsBadRequestException(
        SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, UserAccountKeysData accountKeys, KdfSettings kdfSettings, string orgSsoIdentifier, string masterPasswordHint)
    {
        // Arrange
        user.Key = "existing-key";
        var model = CreateValidModel(user, accountKeys, kdfSettings, orgSsoIdentifier, masterPasswordHint);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, model));
        Assert.Equal("User already has a master password set.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_AccountKeysNull_ThrowsBadRequestException(
        SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, KdfSettings kdfSettings, string orgSsoIdentifier, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        var model = CreateValidModel(user, null, kdfSettings, orgSsoIdentifier, masterPasswordHint);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, model));
        Assert.Equal("Account keys are required.", exception.Message);
    }

    [Theory]
    [BitAutoData("wrong-salt", null)]
    [BitAutoData([null, "wrong-salt"])]
    [BitAutoData("wrong-salt", "different-wrong-salt")]
    public async Task SetInitialMasterPassword_InvalidSalt_ThrowsBadRequestException(
        string? authSaltOverride, string? unlockSaltOverride,
        SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, UserAccountKeysData accountKeys, KdfSettings kdfSettings, string orgSsoIdentifier, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        var correctSalt = user.GetMasterPasswordSalt();
        var model = new SetInitialMasterPasswordDataModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = authSaltOverride ?? correctSalt,
                MasterPasswordAuthenticationHash = "hash",
                Kdf = kdfSettings
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = unlockSaltOverride ?? correctSalt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdfSettings
            },
            AccountKeys = accountKeys,
            OrgSsoIdentifier = orgSsoIdentifier,
            MasterPasswordHint = masterPasswordHint
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, model));
        Assert.Equal("Invalid master password salt.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_InvalidOrgSsoIdentifier_ThrowsBadRequestException(
        SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, UserAccountKeysData accountKeys, KdfSettings kdfSettings, string orgSsoIdentifier, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        var model = CreateValidModel(user, accountKeys, kdfSettings, orgSsoIdentifier, masterPasswordHint);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgSsoIdentifier)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, model));
        Assert.Equal("Organization SSO identifier is invalid.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task SetInitialMasterPassword_UserNotFoundInOrganization_ThrowsBadRequestException(
        SutProvider<SetInitialMasterPasswordCommand> sutProvider,
        User user, UserAccountKeysData accountKeys, KdfSettings kdfSettings, Organization org, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        var model = CreateValidModel(user, accountKeys, kdfSettings, org.Identifier, masterPasswordHint);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(org.Identifier)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.SetInitialMasterPasswordAsync(user, model));
        Assert.Equal("User not found within organization.", exception.Message);
    }

    private static SetInitialMasterPasswordDataModel CreateValidModel(
        User user, UserAccountKeysData? accountKeys, KdfSettings kdfSettings,
        string orgSsoIdentifier, string? masterPasswordHint)
    {
        var salt = user.GetMasterPasswordSalt();
        return new SetInitialMasterPasswordDataModel
        {
            MasterPasswordAuthentication = new MasterPasswordAuthenticationData
            {
                Salt = salt,
                MasterPasswordAuthenticationHash = "hash",
                Kdf = kdfSettings
            },
            MasterPasswordUnlock = new MasterPasswordUnlockData
            {
                Salt = salt,
                MasterKeyWrappedUserKey = "wrapped-key",
                Kdf = kdfSettings
            },
            AccountKeys = accountKeys,
            OrgSsoIdentifier = orgSsoIdentifier,
            MasterPasswordHint = masterPasswordHint
        };
    }
}
