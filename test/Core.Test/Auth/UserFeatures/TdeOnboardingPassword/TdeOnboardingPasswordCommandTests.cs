using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.TdeOnboardingPassword;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TdeOnboardingPassword;

[SutProviderCustomize]
public class TdeOnboardingPasswordCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task OnboardMasterPassword_Success(SutProvider<TdeOnboardingPasswordCommand> sutProvider,
        User user, KdfSettings kdfSettings,
        Organization org, OrganizationUser orgUser, string serverSideHash, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        user.PublicKey = "public-key";
        user.PrivateKey = "private-key";
        var model = CreateValidModel(user, kdfSettings, org.Identifier, masterPasswordHint);

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
        await sutProvider.Sut.OnboardMasterPasswordAsync(user, model);

        // Assert
        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .UpdateUserDataAsync(Arg.Do<IEnumerable<UpdateUserData>>(actions =>
            {
                var actionsList = actions.ToList();
                Assert.Single(actionsList);
                Assert.Same(mockUpdateUserData, actionsList[0]);
            }));

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogUserEventAsync(user.Id, EventType.User_ChangedPassword);
    }

    [Theory]
    [BitAutoData]
    public async Task OnboardMasterPassword_UserAlreadyHasPassword_ThrowsBadRequestException(
        SutProvider<TdeOnboardingPasswordCommand> sutProvider,
        User user, KdfSettings kdfSettings, string orgSsoIdentifier, string masterPasswordHint)
    {
        // Arrange
        user.Key = "existing-key";
        var model = CreateValidModel(user, kdfSettings, orgSsoIdentifier, masterPasswordHint);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.OnboardMasterPasswordAsync(user, model));
        Assert.Equal("User already has a master password set.", exception.Message);
    }

    [Theory]
    [BitAutoData([null, "private-key"])]
    [BitAutoData("public-key", null)]
    [BitAutoData([null, null])]
    public async Task OnboardMasterPassword_MissingAccountKeys_ThrowsBadRequestException(
        string? publicKey, string? privateKey,
        SutProvider<TdeOnboardingPasswordCommand> sutProvider,
        User user, KdfSettings kdfSettings, string orgSsoIdentifier, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        user.PublicKey = publicKey;
        user.PrivateKey = privateKey;
        var model = CreateValidModel(user, kdfSettings, orgSsoIdentifier, masterPasswordHint);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.OnboardMasterPasswordAsync(user, model));
        Assert.Equal("TDE user account keys must be set before setting initial master password.", exception.Message);
    }

    [Theory]
    [BitAutoData("wrong-salt", null)]
    [BitAutoData([null, "wrong-salt"])]
    [BitAutoData("wrong-salt", "different-wrong-salt")]
    public async Task OnboardMasterPassword_InvalidSalt_ThrowsBadRequestException(
        string? authSaltOverride, string? unlockSaltOverride,
        SutProvider<TdeOnboardingPasswordCommand> sutProvider,
        User user, KdfSettings kdfSettings, string orgSsoIdentifier, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        user.PublicKey = "public-key";
        user.PrivateKey = "private-key";
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
            AccountKeys = null,
            OrgSsoIdentifier = orgSsoIdentifier,
            MasterPasswordHint = masterPasswordHint
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.OnboardMasterPasswordAsync(user, model));
        Assert.Equal("Invalid master password salt.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task OnboardMasterPassword_InvalidOrgSsoIdentifier_ThrowsBadRequestException(
        SutProvider<TdeOnboardingPasswordCommand> sutProvider,
        User user, KdfSettings kdfSettings, string orgSsoIdentifier, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        user.PublicKey = "public-key";
        user.PrivateKey = "private-key";
        var model = CreateValidModel(user, kdfSettings, orgSsoIdentifier, masterPasswordHint);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(orgSsoIdentifier)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.OnboardMasterPasswordAsync(user, model));
        Assert.Equal("Organization SSO identifier is invalid.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task OnboardMasterPassword_UserNotFoundInOrganization_ThrowsBadRequestException(
        SutProvider<TdeOnboardingPasswordCommand> sutProvider,
        User user, KdfSettings kdfSettings, Organization org, string masterPasswordHint)
    {
        // Arrange
        user.Key = null;
        user.PublicKey = "public-key";
        user.PrivateKey = "private-key";
        var model = CreateValidModel(user, kdfSettings, org.Identifier, masterPasswordHint);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(org.Identifier)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.OnboardMasterPasswordAsync(user, model));
        Assert.Equal("User not found within organization.", exception.Message);
    }

    private static SetInitialMasterPasswordDataModel CreateValidModel(
        User user, KdfSettings kdfSettings, string orgSsoIdentifier, string? masterPasswordHint)
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
            AccountKeys = null,
            OrgSsoIdentifier = orgSsoIdentifier,
            MasterPasswordHint = masterPasswordHint
        };
    }
}
