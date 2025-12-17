using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

[SutProviderCustomize]
public class SetKeyConnectorKeyCommandTests
{

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_Success_SetsAccountKeys(
        User user,
        KeyConnectorKeysData data,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Set up valid V2 encryption data
        if (data.AccountKeys!.SignatureKeyPair != null)
        {
            data.AccountKeys.SignatureKeyPair.SignatureAlgorithm = "ed25519";
        }

        var expectedAccountKeysData = data.AccountKeys.ToAccountKeysData();

        // Arrange
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var mockUpdateUserData = Substitute.For<UpdateUserData>();
        userRepository.SetKeyConnectorUserKey(user.Id, data.KeyConnectorKeyWrappedUserKey!)
            .Returns(mockUpdateUserData);

        // Act
        await sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data);

        // Assert
        sutProvider.GetDependency<ICanUseKeyConnectorQuery>()
            .Received(1)
            .VerifyCanUseKeyConnector(user);

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
                    actions.Count() == 1 && actions.First() == mockUpdateUserData));

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogUserEventAsync(user.Id, EventType.User_MigratedKeyToKeyConnector);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .Received(1)
            .AcceptOrgUserByOrgSsoIdAsync(data.OrgIdentifier, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory, BitAutoData]
    public async Task SetKeyConnectorKeyForUserAsync_UserCannotUseKeyConnector_ThrowsException(
        User user,
        KeyConnectorKeysData data,
        SutProvider<SetKeyConnectorKeyCommand> sutProvider)
    {
        // Arrange
        var expectedException = new BadRequestException("User cannot use Key Connector");
        sutProvider.GetDependency<ICanUseKeyConnectorQuery>()
            .When(x => x.VerifyCanUseKeyConnector(user))
            .Throw(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SetKeyConnectorKeyForUserAsync(user, data));

        Assert.Equal(expectedException.Message, exception.Message);

        sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetKeyConnectorUserKey(Arg.Any<Guid>(), Arg.Any<string>());

        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .SetV2AccountCryptographicStateAsync(Arg.Any<Guid>(), Arg.Any<UserAccountKeysData>(), Arg.Any<IEnumerable<UpdateUserData>>());

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogUserEventAsync(Arg.Any<Guid>(), Arg.Any<EventType>());

        await sutProvider.GetDependency<IAcceptOrgUserCommand>()
            .DidNotReceiveWithAnyArgs()
            .AcceptOrgUserByOrgSsoIdAsync(Arg.Any<string>(), Arg.Any<User>(), Arg.Any<IUserService>());
    }
}
