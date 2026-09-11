using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

[SutProviderCustomize]
public class WebAuthnTokenProviderTests
{
    [Theory, BitAutoData]
    public async Task CanGenerateTwoFactorTokenAsync_NoProvider_ReturnsFalse(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider)
    {
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>());

        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(SubstituteUserManager(), user);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task CanGenerateTwoFactorTokenAsync_EmptyMetaData_ReturnsFalse(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider)
    {
        SetupProvider(user, enabled: true, metaData: new Dictionary<string, object>());

        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(SubstituteUserManager(), user);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task CanGenerateTwoFactorTokenAsync_HasMetaData_ReturnsProviderEnabledState(bool enabled,
        User user, SutProvider<WebAuthnTokenProvider> sutProvider)
    {
        SetupProvider(user, enabled, metaData: new Dictionary<string, object>
        {
            ["Key1"] = CreateCredential([1, 2, 3])
        });

        var result = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(SubstituteUserManager(), user);

        Assert.Equal(enabled, result);
    }

    [Theory, BitAutoData]
    public async Task GenerateAsync_NoExistingCredentials_ReturnsNull(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider)
    {
        SetupUserService(sutProvider);
        SetupProvider(user, enabled: true, metaData: new Dictionary<string, object>());

        var token = await sutProvider.Sut.GenerateAsync("purpose", SubstituteUserManager(), user);

        Assert.Null(token);
    }

    [Theory, BitAutoData]
    public async Task GenerateAsync_WithExistingCredentials_ReturnsOptionsAndUpdatesProvider(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider, AssertionOptions assertionOptions)
    {
        var userService = SetupUserService(sutProvider);
        SetupProvider(user, enabled: true, metaData: new Dictionary<string, object>
        {
            ["Key1"] = CreateCredential([1, 2, 3])
        });

        sutProvider.GetDependency<IFido2>()
            .GetAssertionOptions(Arg.Any<GetAssertionOptionsParams>())
            .Returns(assertionOptions);

        var token = await sutProvider.Sut.GenerateAsync("purpose", SubstituteUserManager(), user);

        Assert.Equal(assertionOptions.ToJson(), token);
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        Assert.True(provider.MetaData.ContainsKey("login"));
        await userService.Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, setEnabled: true, logEvent: false);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EmptyToken_ReturnsFalse(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider)
    {
        SetupUserService(sutProvider);

        var result = await sutProvider.Sut.ValidateAsync("purpose", "   ", SubstituteUserManager(), user);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NoLoginMetaData_ReturnsFalse(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider, AuthenticatorAssertionRawResponse response)
    {
        SetupUserService(sutProvider);
        SetupProvider(user, enabled: true, metaData: new Dictionary<string, object>
        {
            ["Key1"] = CreateCredential(response.RawId)
        });

        var result = await sutProvider.Sut.ValidateAsync("purpose", JsonSerializer.Serialize(response),
            SubstituteUserManager(), user);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NoMatchingCredential_ReturnsFalse(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider, AuthenticatorAssertionRawResponse response,
        AssertionOptions options)
    {
        SetupUserService(sutProvider);
        SetupProvider(user, enabled: true, metaData: new Dictionary<string, object>
        {
            ["Key1"] = CreateCredential([9, 9, 9]),
            ["login"] = options.ToJson()
        });

        var result = await sutProvider.Sut.ValidateAsync("purpose", JsonSerializer.Serialize(response),
            SubstituteUserManager(), user);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AssertionSucceeds_ReturnsTrueAndUpdatesCredential(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider, AuthenticatorAssertionRawResponse response,
        AssertionOptions options)
    {
        var userService = SetupUserService(sutProvider);
        var credential = CreateCredential(response.RawId);
        SetupProvider(user, enabled: true, metaData: new Dictionary<string, object>
        {
            ["Key1"] = credential,
            ["login"] = options.ToJson()
        });

        sutProvider.GetDependency<IFido2>()
            .MakeAssertionAsync(Arg.Any<MakeAssertionParams>(), Arg.Any<CancellationToken>())
            .Returns(new VerifyAssertionResult { CredentialId = response.RawId, SignCount = 42 });

        var result = await sutProvider.Sut.ValidateAsync("purpose", JsonSerializer.Serialize(response),
            SubstituteUserManager(), user);

        Assert.True(result);
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        Assert.False(provider.MetaData.ContainsKey("login"));
        // ValidateAsync rebuilds the credential via LoadKeys rather than mutating the original instance,
        // so the updated counter must be read back from metadata rather than from `credential`.
        var updatedCredential = Assert.IsType<TwoFactorProvider.WebAuthnData>(provider.MetaData["Key1"]);
        Assert.Equal(42u, updatedCredential.SignatureCounter);
        await userService.Received(1)
            .UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.WebAuthn, setEnabled: true, logEvent: false);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AssertionThrowsFido2VerificationException_ReturnsFalse(
        User user, SutProvider<WebAuthnTokenProvider> sutProvider, AuthenticatorAssertionRawResponse response,
        AssertionOptions options)
    {
        SetupUserService(sutProvider);
        SetupProvider(user, enabled: true, metaData: new Dictionary<string, object>
        {
            ["Key1"] = CreateCredential(response.RawId),
            ["login"] = options.ToJson()
        });

        sutProvider.GetDependency<IFido2>()
            .MakeAssertionAsync(Arg.Any<MakeAssertionParams>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<Fido2VerificationException>();

        var result = await sutProvider.Sut.ValidateAsync("purpose", JsonSerializer.Serialize(response),
            SubstituteUserManager(), user);

        Assert.False(result);
    }

    private static IUserService SetupUserService(SutProvider<WebAuthnTokenProvider> sutProvider)
    {
        var userService = Substitute.For<IUserService>();
        sutProvider.GetDependency<IServiceProvider>()
            .GetService(typeof(IUserService))
            .Returns(userService);
        return userService;
    }

    private static void SetupProvider(User user, bool enabled, Dictionary<string, object> metaData)
    {
        var provider = new TwoFactorProvider { Enabled = enabled, MetaData = metaData };
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.WebAuthn] = provider
        });
    }

    private static TwoFactorProvider.WebAuthnData CreateCredential(byte[] credentialId)
    {
        return new TwoFactorProvider.WebAuthnData
        {
            Name = "Key1",
            Descriptor = new PublicKeyCredentialDescriptor(credentialId),
            PublicKey = [4, 5, 6],
            UserHandle = [7, 8, 9],
            SignatureCounter = 0,
            CredType = "public-key",
            RegDate = DateTime.UtcNow,
            AaGuid = Guid.NewGuid()
        };
    }

    private static UserManager<User> SubstituteUserManager()
    {
        return new UserManager<User>(Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
    }
}
