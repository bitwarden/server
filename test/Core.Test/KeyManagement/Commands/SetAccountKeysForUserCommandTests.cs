using Bit.Core.Entities;
using Bit.Core.KeyManagement.Commands;
using Bit.Core.KeyManagement.Entities;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.Commands;

public class SetAccountKeysForUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task SetAccountKeysForUserAsync_WithV1Keys_UpdatesUserCorrectlyAsync(
        User user,
        AccountKeysRequestModel accountKeys)
    {
        accountKeys.PublicKeyEncryptionKeyPair = null;
        accountKeys.SignatureKeyPair = null;
        accountKeys.SecurityState = null;

        // Clear any signature-related properties set by autofixture
        user.SignedPublicKey = null;
        user.SecurityState = null;
        user.SecurityVersion = null;

        var userRepository = Substitute.For<IUserRepository>();
        var userSignatureKeyPairRepository = Substitute.For<IUserSignatureKeyPairRepository>();
        var command = new SetAccountKeysForUserCommand(userRepository, userSignatureKeyPairRepository);

        await command.SetAccountKeysForUserAsync(user, accountKeys);

        Assert.Equal(accountKeys.UserKeyEncryptedAccountPrivateKey, user.PrivateKey);
        Assert.Equal(accountKeys.AccountPublicKey, user.PublicKey);
        Assert.Null(user.SignedPublicKey);
        Assert.Null(user.SecurityState);
        Assert.Null(user.SecurityVersion);

        await userRepository
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.Id == user.Id));

        await userSignatureKeyPairRepository
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<UserSignatureKeyPair>());
    }

    [Theory]
    [BitAutoData]
    public async Task SetAccountKeysForUserAsync_WithV2Keys_UpdatesUserAndCreatesSignatureKeyPairAsync(
        User user)
    {
        var publicKeyEncryptionKeyPair = new PublicKeyEncryptionKeyPairRequestModel
        {
            WrappedPrivateKey = "wrappedPrivateKey",
            PublicKey = "publicKey",
            SignedPublicKey = "signedPublicKey"
        };

        var signatureKeyPair = new SignatureKeyPairRequestModel
        {
            SignatureAlgorithm = "ed25519",
            WrappedSigningKey = "wrappedSigningKey",
            VerifyingKey = "verifyingKey"
        };

        var securityState = new SecurityStateModel
        {
            SecurityState = "state",
            SecurityVersion = 1
        };

        var accountKeys = new AccountKeysRequestModel
        {
            UserKeyEncryptedAccountPrivateKey = "userKeyEncryptedPrivateKey",
            AccountPublicKey = "accountPublicKey",
            PublicKeyEncryptionKeyPair = publicKeyEncryptionKeyPair,
            SignatureKeyPair = signatureKeyPair,
            SecurityState = securityState
        };

        var userRepository = Substitute.For<IUserRepository>();
        var userSignatureKeyPairRepository = Substitute.For<IUserSignatureKeyPairRepository>();
        var command = new SetAccountKeysForUserCommand(userRepository, userSignatureKeyPairRepository);

        await command.SetAccountKeysForUserAsync(user, accountKeys);

        Assert.Equal(publicKeyEncryptionKeyPair.WrappedPrivateKey, user.PrivateKey);
        Assert.Equal(publicKeyEncryptionKeyPair.PublicKey, user.PublicKey);
        Assert.Equal(publicKeyEncryptionKeyPair.SignedPublicKey, user.SignedPublicKey);
        Assert.Equal(securityState.SecurityState, user.SecurityState);
        Assert.Equal(securityState.SecurityVersion, user.SecurityVersion);

        await userRepository
            .Received(1)
            .ReplaceAsync(Arg.Is<User>(u => u.Id == user.Id));

        await userSignatureKeyPairRepository
            .Received(1)
            .CreateAsync(Arg.Is<UserSignatureKeyPair>(pair =>
                pair.UserId == user.Id &&
                pair.SignatureAlgorithm == SignatureAlgorithm.Ed25519 &&
                pair.SigningKey == signatureKeyPair.WrappedSigningKey &&
                pair.VerifyingKey == signatureKeyPair.VerifyingKey));
    }
}
