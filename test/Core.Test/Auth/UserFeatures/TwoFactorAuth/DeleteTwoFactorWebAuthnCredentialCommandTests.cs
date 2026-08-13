using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib.Objects;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.TwoFactorAuth;

[SutProviderCustomize]
public class DeleteTwoFactorWebAuthnCredentialCommandTests
{
    private static void SetupWebAuthnProvider(User user, int credentialCount)
    {
        var providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>();
        var metadata = new Dictionary<string, object>();

        // Add credentials as Key1, Key2, Key3, etc.
        for (var i = 1; i <= credentialCount; i++)
        {
            metadata[$"Key{i}"] = new TwoFactorProvider.WebAuthnData
            {
                Name = $"Key {i}",
                Descriptor = new PublicKeyCredentialDescriptor([(byte)i]),
                PublicKey = [(byte)i],
                UserHandle = [(byte)i],
                SignatureCounter = 0,
                CredType = "public-key",
                RegDate = DateTime.UtcNow,
                AaGuid = Guid.NewGuid()
            };
        }

        providers[TwoFactorProviderType.WebAuthn] = new TwoFactorProvider { Enabled = true, MetaData = metadata };

        user.SetTwoFactorProviders(providers);
    }

    /// <summary>
    /// When the user has multiple WebAuthn credentials and requests deletion of an existing key,
    /// the command should remove it, persist via UserService, and return true.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAsync_KeyExistsWithMultipleKeys_RemovesKeyAndReturnsTrue(
        SutProvider<DeleteTwoFactorWebAuthnCredentialCommand> sutProvider, User user)
    {
        // Arrange
        SetupWebAuthnProvider(user, 3);
        var keyIdToDelete = 2;

        // Act
        var result = await sutProvider.Sut.DeleteTwoFactorWebAuthnCredentialAsync(user, keyIdToDelete);

        // Assert
        Assert.True(result);

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        Assert.NotNull(provider?.MetaData);
        Assert.False(provider.MetaData.ContainsKey($"Key{keyIdToDelete}"));
        Assert.Equal(2, provider.MetaData.Count);

        await sutProvider.GetDependency<IUserService>().Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn);
    }

    /// <summary>
    /// When the requested key does not exist, the command should return false
    /// and not call UserService.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAsync_KeyDoesNotExist_ReturnsFalse(
        SutProvider<DeleteTwoFactorWebAuthnCredentialCommand> sutProvider, User user)
    {
        // Arrange
        SetupWebAuthnProvider(user, 2);
        var nonExistentKeyId = 99;

        // Act
        var result = await sutProvider.Sut.DeleteTwoFactorWebAuthnCredentialAsync(user, nonExistentKeyId);

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IUserService>().DidNotReceive()
            .UpdateTwoFactorProviderAsync(Arg.Any<User>(), Arg.Any<TwoFactorProviderType>());
    }

    /// <summary>
    /// Users must retain at least one WebAuthn credential. When only one key remains,
    /// deletion should be rejected to prevent lockout.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAsync_OnlyOneKeyRemaining_ReturnsFalse(
        SutProvider<DeleteTwoFactorWebAuthnCredentialCommand> sutProvider, User user)
    {
        // Arrange
        SetupWebAuthnProvider(user, 1);
        var keyIdToDelete = 1;

        // Act
        var result = await sutProvider.Sut.DeleteTwoFactorWebAuthnCredentialAsync(user, keyIdToDelete);

        // Assert
        Assert.False(result);

        // Key should still exist
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        Assert.NotNull(provider?.MetaData);
        Assert.True(provider.MetaData.ContainsKey($"Key{keyIdToDelete}"));

        await sutProvider.GetDependency<IUserService>().DidNotReceive()
            .UpdateTwoFactorProviderAsync(Arg.Any<User>(), Arg.Any<TwoFactorProviderType>());
    }

    /// <summary>
    /// When the user has no two-factor providers configured, deletion should return false.
    /// </summary>
    [Theory, BitAutoData]
    public async Task DeleteAsync_NoProviders_ReturnsFalse(
        SutProvider<DeleteTwoFactorWebAuthnCredentialCommand> sutProvider, User user)
    {
        // Arrange - user with no providers (clear any AutoFixture-generated ones)
        user.SetTwoFactorProviders(null);

        // Act
        var result = await sutProvider.Sut.DeleteTwoFactorWebAuthnCredentialAsync(user, 1);

        // Assert
        Assert.False(result);

        await sutProvider.GetDependency<IUserService>().DidNotReceive()
            .UpdateTwoFactorProviderAsync(Arg.Any<User>(), Arg.Any<TwoFactorProviderType>());
    }
}
