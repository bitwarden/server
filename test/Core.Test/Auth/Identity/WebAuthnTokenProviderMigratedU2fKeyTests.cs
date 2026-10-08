using System.Text.Json;
using System.Text.Json.Nodes;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.Fakes;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

/// <summary>
/// Runs against the real Fido2 library (no <see cref="IFido2"/> mock) with a YubiKey registered as
/// U2F and migrated to WebAuthn. The tests assert that two-factor login works for that key and that the server
/// applies its own U2F AppID. The test <c>MakeAssertionAsync_MigratedU2fKeyWithStoredOptions_ThrowsInvalidRpidHash</c>
/// documents the Fido2 4.0.1 library behavior (AppID is not serialized), not provider behavior.
/// </summary>
public class WebAuthnTokenProviderMigratedU2fKeyTests
{
    private const string _vaultUrl = "https://vault.bitwarden.com";
    private const string _rpId = "vault.bitwarden.com";
    private const string _u2fAppIdUrl = "https://vault.bitwarden.com/app-id.json";
    private const string _foreignAppIdUrl = "https://evil.example/app-id.json";

    [Fact]
    public async Task MakeAssertionAsync_MigratedU2fKeyWithStoredOptions_ThrowsInvalidRpidHash()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;
        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        var storedLogin = (string)harness.User.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn)!.MetaData["login"];
        var key = LoadStoredKey(harness.User);

        // Same inputs ValidateAsync passes to the library: options rebuilt from the stored challenge.
        var storedOptions = AssertionOptions.FromJson(storedLogin);
        Assert.Null(storedOptions.Extensions?.AppID);

        var exception = await Assert.ThrowsAsync<Fido2VerificationException>(() =>
            harness.Fido2.MakeAssertionAsync(CreateMakeAssertionParams(assertion, storedOptions, key)));
        Assert.Equal("InvalidRpidHash", exception.Code.ToString());

        // Counterfactual: identical assertion and key, but the options still carry the AppID. This passes,
        // so the lost AppID extension is the only reason the stored options fail.
        var optionsWithAppId = AssertionOptions.FromJson(storedLogin);
        optionsWithAppId.Extensions = new AuthenticationExtensionsClientInputs { AppID = _u2fAppIdUrl };

        var result = await harness.Fido2.MakeAssertionAsync(CreateMakeAssertionParams(assertion, optionsWithAppId, key));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ValidateAsync_NativeWebAuthnKeyAssertionScopedToRpId_ReturnsTrue()
    {
        using var authenticator = new FakeWebAuthnAuthenticator();
        var harness = CreateHarness(CreateNativeWebAuthnUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: [7, 8, 9]);
        var token = FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", token, SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task GenerateAsync_MigratedU2fKey_ClientOptionsCarryAppIdExtensionOnce()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);

        Assert.NotNull(optionsJson);
        AssertExtensionsCarryAppIdOnce(optionsJson);
    }

    [Fact]
    public async Task GenerateAsync_MigratedU2fKey_StoredChallengeCarriesAppIdExtension()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);

        var storedLogin = (string)harness.User.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn)!.MetaData["login"];
        AssertExtensionsCarryAppIdOnce(storedLogin);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyAssertionScopedToAppId_ReturnsTrue()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task TwoFactorLogin_MigratedU2fKeyResavedAfterMigration_ReturnsTrue()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateResavedMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyWithChallengeStoredWithoutAppId_ReturnsTrue()
    {
        // ValidateAsync must always apply the server's own U2F AppID, also after a Fido2 version that serializes it again.
        // The AppID never comes from the stored challenge or the client, so challenges stored without one still validate.
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        // A challenge in the shape written before the fix: serialized options without the AppID extension.
        var legacyOptions = harness.Fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [new PublicKeyCredentialDescriptor(authenticator.CredentialId)],
            UserVerification = UserVerificationRequirement.Discouraged,
            Extensions = new AuthenticationExtensionsClientInputs { UserVerificationMethod = true },
        });
        var legacyLogin = JsonSerializer.Serialize(legacyOptions);
        Assert.DoesNotContain("appid", legacyLogin, StringComparison.OrdinalIgnoreCase);

        var providers = harness.User.GetTwoFactorProviders();
        providers[TwoFactorProviderType.WebAuthn].MetaData["login"] = legacyLogin;
        harness.User.SetTwoFactorProviders(providers);

        var assertion = authenticator.MakeAssertion(legacyOptions.Challenge, _rpId, _vaultUrl, userHandle: null,
            appId: _u2fAppIdUrl);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyAssertionScopedToForeignAppId_ReturnsFalse()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null,
            appId: _foreignAppIdUrl);
        Assert.True(assertion.ClientExtensionResults.AppID);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyWithForeignAppIdInStoredChallenge_AssertionScopedToForeignAppId_ReturnsFalse()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;
        ReplaceStoredChallengeAppId(harness.User, _foreignAppIdUrl);

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _foreignAppIdUrl);
        Assert.True(assertion.ClientExtensionResults.AppID);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyWithForeignAppIdInStoredChallenge_AssertionScopedToServerAppId_ReturnsTrue()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;
        ReplaceStoredChallengeAppId(harness.User, _foreignAppIdUrl);

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyAppIdScopedAssertionWithoutAppIdExtensionResult_ReturnsFalse()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        assertion.ClientExtensionResults.AppID = false;
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyAssertionScopedToAppId_UpdatesStoredSignatureCounter()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };
        Assert.Equal(FakeWebAuthnAuthenticator.CarriedOverU2fCounter, LoadStoredKey(userFromStorage).SignatureCounter);

        var result = await harness.Provider.ValidateAsync("TwoFactor", FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
        Assert.Equal(authenticator.SignatureCounter, LoadStoredKey(userFromStorage).SignatureCounter);
        Assert.True(authenticator.SignatureCounter > FakeWebAuthnAuthenticator.CarriedOverU2fCounter);
    }

    [Fact]
    public void WebClientTokenString_CarriesAppIdExtensionResultIntoAssertionResponse()
    {
        // Guards the round trip test: the extension result the web client sends under the "extensions" key
        // must reach the Fido2 model, otherwise the failure above could be blamed on a dropped result.
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var assertion = authenticator.MakeAssertion([1, 2, 3], _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);

        var parsed = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
            FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.True(parsed!.ClientExtensionResults.AppID);
    }

    private static void ReplaceStoredChallengeAppId(User user, string appId)
    {
        var providers = user.GetTwoFactorProviders();
        var login = JsonNode.Parse((string)providers[TwoFactorProviderType.WebAuthn].MetaData["login"])!.AsObject();
        login["extensions"]!.AsObject()["appid"] = appId;
        providers[TwoFactorProviderType.WebAuthn].MetaData["login"] = login.ToJsonString();
        user.SetTwoFactorProviders(providers);
    }

    private static void AssertExtensionsCarryAppIdOnce(string json)
    {
        var occurrences = System.Text.RegularExpressions.Regex.Count(json, "\"appid\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.Equal(1, occurrences);

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("extensions", out var extensions));
        Assert.True(extensions.TryGetProperty("appid", out var appId));
        Assert.Equal(_u2fAppIdUrl, appId.GetString());
    }

    private static MakeAssertionParams CreateMakeAssertionParams(AuthenticatorAssertionRawResponse assertion,
        AssertionOptions options, TwoFactorProvider.WebAuthnData key)
    {
        return new MakeAssertionParams
        {
            AssertionResponse = assertion,
            OriginalOptions = options,
            StoredPublicKey = key.PublicKey,
            StoredSignatureCounter = key.SignatureCounter,
            IsUserHandleOwnerOfCredentialIdCallback = (_, _) => Task.FromResult(true),
        };
    }

    private static TwoFactorProvider.WebAuthnData LoadStoredKey(User user)
    {
        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn)!;
        return new TwoFactorProvider.WebAuthnData((dynamic)provider.MetaData["Key1"]);
    }

    private static Harness CreateHarness(User user)
    {
        var globalSettings = new GlobalSettings();
        globalSettings.BaseServiceUri.Vault = _vaultUrl;

        // Copy of ServiceCollectionExtensions.AddWebAuthn, because Core.Test does not reference SharedWeb.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFido2(options =>
        {
            options.ServerDomain = new Uri(globalSettings.BaseServiceUri.Vault).Host;
            options.ServerName = "Bitwarden";
            options.TimestampDriftTolerance = 300000;

            if (globalSettings.Fido2?.Origins?.Any() == true)
            {
                options.Origins = new HashSet<string>(globalSettings.Fido2.Origins);
            }
            else
            {
                options.Origins = new HashSet<string> {
                    globalSettings.BaseServiceUri.Vault,
                    Constants.BrowserExtensions.ChromeId,
                    Constants.BrowserExtensions.ChromeBetaId,
                    Constants.BrowserExtensions.EdgeId,
                    Constants.BrowserExtensions.OperaId
                 };
            }
        });
        var fido2 = services.BuildServiceProvider().GetRequiredService<IFido2>();

        var userService = Substitute.For<IUserService>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUserService)).Returns(userService);

        var provider = new WebAuthnTokenProvider(serviceProvider, fido2, globalSettings);
        return new Harness(provider, fido2, globalSettings, user);
    }

    /// <summary>
    /// A user whose <c>TwoFactorProviders</c> JSON is exactly what the 2020 U2F-to-WebAuthn migration wrote.
    /// </summary>
    private static User CreateMigratedU2fUser(FakeWebAuthnAuthenticator authenticator)
    {
        return new User
        {
            TwoFactorProviders = authenticator.GetMigratedU2fTwoFactorProvidersJson("YubiKey 5 NFC",
                FakeWebAuthnAuthenticator.CarriedOverU2fCounter),
        };
    }

    /// <summary>
    /// A user whose migrated key the server saved again after a successful two-factor login.
    /// </summary>
    private static User CreateResavedMigratedU2fUser(FakeWebAuthnAuthenticator authenticator)
    {
        return new User
        {
            TwoFactorProviders = authenticator.GetResavedMigratedU2fTwoFactorProvidersJson("YubiKey 5 NFC",
                FakeWebAuthnAuthenticator.CarriedOverU2fCounter),
        };
    }

    private static User CreateNativeWebAuthnUser(FakeWebAuthnAuthenticator authenticator)
    {
        var key = new TwoFactorProvider.WebAuthnData
        {
            Name = "Native key",
            Descriptor = new PublicKeyCredentialDescriptor(authenticator.CredentialId),
            PublicKey = authenticator.GetCosePublicKey(),
            UserHandle = [7, 8, 9],
            SignatureCounter = 0,
            CredType = "public-key",
            RegDate = DateTime.UtcNow,
            AaGuid = Guid.NewGuid(),
            Migrated = false,
        };

        return CreateUserWithStoredKey(key);
    }

    /// <summary>
    /// Stores the key through <c>User.SetTwoFactorProviders</c> (Newtonsoft) and returns a different
    /// <see cref="User"/> instance that only has the persisted JSON, so the key is read back through
    /// <c>User.GetTwoFactorProvider</c> the way production reads it.
    /// </summary>
    private static User CreateUserWithStoredKey(TwoFactorProvider.WebAuthnData key)
    {
        var writer = new User();
        writer.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.WebAuthn] = new()
            {
                Enabled = true,
                MetaData = new Dictionary<string, object> { ["Key1"] = key },
            },
        });

        return new User { TwoFactorProviders = writer.TwoFactorProviders };
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

    private sealed record Harness(WebAuthnTokenProvider Provider, IFido2 Fido2, GlobalSettings GlobalSettings, User User);
}
