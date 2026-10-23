using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

[SutProviderCustomize]
public class SetKeyConnectorKeyCommandTests
{
    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_Success_SetsAccountKeys(
        User user,
        KeyConnectorKeysData data,
        Organization org,
        OrganizationUser orgUser,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        data = WithOrgIdentifier(data, org.Identifier!, data.ContainedKeyId);
        var expectedAccountKeysData = data.AccountKeys.ToAccountKeysData();

        user.UsesKeyConnector = false;
        SetupAuthorization(sutProvider, user, succeeded: true);
        SetupOrganizationUser(sutProvider, user, org, orgUser);

        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var mockUpdateUserData = Substitute.For<UpdateUserData>();
        userRepository.SetKeyConnectorUserKey(user.Id, data.KeyConnectorKeyWrappedUserKey!)
            .Returns(mockUpdateUserData);
        var mockSetUserKeyId = Substitute.For<UpdateUserData>();
        userRepository.SetUserKeyId(user.Id, data.ContainedKeyId!)
            .Returns(mockSetUserKeyId);

        // Act
        await sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data);

        // Assert
        userRepository
            .Received(1)
            .SetKeyConnectorUserKey(user.Id, data.KeyConnectorKeyWrappedUserKey);

        await userRepository
            .Received(1)
            .SetV2AccountCryptographicStateAsync(
                user.Id,
                Arg.Is<UserAccountKeysData>(data =>
                    data.PublicKeyEncryptionKeyPairData.PublicKey == expectedAccountKeysData.PublicKeyEncryptionKeyPairData.PublicKey &&
                    data.PublicKeyEncryptionKeyPairData.WrappedPrivateKey == expectedAccountKeysData.PublicKeyEncryptionKeyPairData.WrappedPrivateKey &&
                    data.PublicKeyEncryptionKeyPairData.SignedPublicKey == expectedAccountKeysData.PublicKeyEncryptionKeyPairData.SignedPublicKey &&
                    data.SignatureKeyPairData!.SignatureAlgorithm == expectedAccountKeysData.SignatureKeyPairData!.SignatureAlgorithm &&
                    data.SignatureKeyPairData.WrappedSigningKey == expectedAccountKeysData.SignatureKeyPairData.WrappedSigningKey &&
                    data.SignatureKeyPairData.VerifyingKey == expectedAccountKeysData.SignatureKeyPairData.VerifyingKey &&
                    data.SecurityStateData!.SecurityState == expectedAccountKeysData.SecurityStateData!.SecurityState &&
                    data.SecurityStateData.SecurityVersion == expectedAccountKeysData.SecurityStateData.SecurityVersion),
                Arg.Is<IEnumerable<UpdateUserData>>(actions =>
                    actions.Count() == 2 &&
                    actions.First() == mockUpdateUserData &&
                    actions.Last() == mockSetUserKeyId));

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        // The default for acceptOrganizationInvite is true.
        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .Received(1)
            .AcceptOrgUserAsync(orgUser, user, sutProvider.GetDependency<IUserService>());
        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgSsoIdAsync(default!, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_AcceptOrganizationInviteTrue_AcceptsOrgUser(
        User user,
        KeyConnectorKeysData data,
        Organization org,
        OrganizationUser orgUser,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        data = WithOrgIdentifier(data, org.Identifier!, data.ContainedKeyId);
        SetupAuthorization(sutProvider, user, succeeded: true);
        SetupOrganizationUser(sutProvider, user, org, orgUser);

        // Act
        await sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data, acceptOrganizationInvite: true);

        // Assert
        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .Received(1)
            .AcceptOrgUserAsync(orgUser, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_AcceptOrganizationInviteFalse_SetsKeysAndDoesNotAcceptOrgUser(
        User user,
        KeyConnectorKeysData data,
        Organization org,
        OrganizationUser orgUser,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        data = WithOrgIdentifier(data, org.Identifier!, data.ContainedKeyId);
        SetupAuthorization(sutProvider, user, succeeded: true);
        SetupOrganizationUser(sutProvider, user, org, orgUser);

        // Act
        await sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data, acceptOrganizationInvite: false);

        // Assert
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .SetV2AccountCryptographicStateAsync(user.Id, Arg.Any<UserAccountKeysData>(),
                Arg.Any<IEnumerable<UpdateUserData>>());

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .AcceptOrgUserAsync(default!, default!, default!);
        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgSsoIdAsync(default!, default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_NoKeyIdSupplied_DoesNotSetUserKeyId(
        User user,
        KeyConnectorKeysData data,
        Organization org,
        OrganizationUser orgUser,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange - a client that predates the key id field sends none
        data = WithOrgIdentifier(data, org.Identifier!, containedKeyId: null);

        user.UsesKeyConnector = false;
        SetupAuthorization(sutProvider, user, succeeded: true);
        SetupOrganizationUser(sutProvider, user, org, orgUser);

        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var mockUpdateUserData = Substitute.For<UpdateUserData>();
        userRepository.SetKeyConnectorUserKey(user.Id, data.KeyConnectorKeyWrappedUserKey!)
            .Returns(mockUpdateUserData);

        // Act
        await sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data);

        // Assert
        userRepository
            .DidNotReceive()
            .SetUserKeyId(Arg.Any<Guid>(), Arg.Any<KeyId>());

        await userRepository
            .Received(1)
            .SetV2AccountCryptographicStateAsync(
                user.Id,
                Arg.Any<UserAccountKeysData>(),
                Arg.Is<IEnumerable<UpdateUserData>>(actions =>
                    actions.Count() == 1 && actions.First() == mockUpdateUserData));
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_UserCantUseKeyConnector_ThrowsException(
        User user,
        KeyConnectorKeysData data,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        user.UsesKeyConnector = true;
        SetupAuthorization(sutProvider, user, succeeded: false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data));
        Assert.Equal("Cannot use Key Connector", exception.Message);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdentifierAsync(default!);
        await AssertNoWritesAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task SetKeyConnectorKeyForUserAsync_OrganizationNotFound_ThrowsAndDoesNotWrite(
        bool acceptOrganizationInvite,
        User user,
        KeyConnectorKeysData data,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        SetupAuthorization(sutProvider, user, succeeded: true);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(data.OrgIdentifier)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data, acceptOrganizationInvite));
        Assert.Equal("Organization invalid.", exception.Message);

        await AssertNoWritesAsync(sutProvider);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task SetKeyConnectorKeyForUserAsync_UserNotFoundInOrganization_ThrowsAndDoesNotWrite(
        bool acceptOrganizationInvite,
        User user,
        KeyConnectorKeysData data,
        Organization org,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        data = WithOrgIdentifier(data, org.Identifier!, data.ContainedKeyId);
        SetupAuthorization(sutProvider, user, succeeded: true);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(org.Identifier!)
            .Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .ReturnsNull();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data, acceptOrganizationInvite));
        Assert.Equal("User not found within organization.", exception.Message);

        await AssertNoWritesAsync(sutProvider);
    }

    private static KeyConnectorKeysData WithOrgIdentifier(KeyConnectorKeysData data, string orgIdentifier,
        KeyId? containedKeyId)
    {
        // Set up valid V2 encryption data
        if (data.AccountKeys.SignatureKeyPair != null)
        {
            data.AccountKeys.SignatureKeyPair.SignatureAlgorithm = "ed25519";
        }

        return new KeyConnectorKeysData
        {
            KeyConnectorKeyWrappedUserKey = data.KeyConnectorKeyWrappedUserKey,
            AccountKeys = data.AccountKeys,
            OrgIdentifier = orgIdentifier,
            ContainedKeyId = containedKeyId
        };
    }

    private static void SetupAuthorization(SutProvider<SetKeyConnectorKeyCommand> sutProvider, User user,
        bool succeeded)
    {
        var httpContext = Substitute.For<HttpContext>();
        httpContext.User.Returns(new ClaimsPrincipal());
        sutProvider.GetDependency<ICurrentContext>().HttpContext.Returns(httpContext);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), user, Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(succeeded ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    private static void SetupOrganizationUser(SutProvider<SetKeyConnectorKeyCommand> sutProvider, User user,
        Organization org, OrganizationUser orgUser)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdentifierAsync(org.Identifier!)
            .Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(org.Id, user.Id)
            .Returns(orgUser);
    }

    private static async Task AssertNoWritesAsync(SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        userRepository.DidNotReceiveWithAnyArgs().SetKeyConnectorUserKey(default, default!);
        userRepository.DidNotReceiveWithAnyArgs().SetUserKeyId(default, default!);
        await userRepository.DidNotReceiveWithAnyArgs()
            .SetV2AccountCryptographicStateAsync(default, default!, default!);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(default, default);

        var acceptOrgUserCommand = sutProvider.GetDependency<IAcceptOrgUserCommand>();
        await acceptOrgUserCommand.DidNotReceiveWithAnyArgs().AcceptOrgUserAsync(default!, default!, default!);
        await acceptOrgUserCommand.DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgSsoIdAsync(default!, default!, default!);
    }
}
