using System.Buffers.Text;
using System.Formats.Cbor;
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
/// PM-44658. Runs against the real Fido2 library (no <see cref="IFido2"/> mock) with a YubiKey registered as
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
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
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
        var token = BuildWebClientTokenString(assertion);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", token, SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task GenerateAsync_MigratedU2fKey_ClientOptionsCarryAppIdExtensionOnce()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);

        Assert.NotNull(optionsJson);
        AssertExtensionsCarryAppIdOnce(optionsJson);
    }

    [Fact]
    public async Task GenerateAsync_MigratedU2fKey_StoredChallengeCarriesAppIdExtension()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);

        var storedLogin = (string)harness.User.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn)!.MetaData["login"];
        AssertExtensionsCarryAppIdOnce(storedLogin);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyAssertionScopedToAppId_ReturnsTrue()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", BuildWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyWithChallengeStoredWithoutAppId_ReturnsTrue()
    {
        // ValidateAsync must always apply the server's own U2F AppID, also after a Fido2 version that serializes it again.
        // The AppID never comes from the stored challenge or the client, so challenges stored without one still validate.
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
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

        var result = await harness.Provider.ValidateAsync("TwoFactor", BuildWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyAssertionScopedToForeignAppId_ReturnsFalse()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null,
            appId: _foreignAppIdUrl);
        Assert.True(assertion.ClientExtensionResults.AppID);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", BuildWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyWithForeignAppIdInStoredChallenge_AssertionScopedToForeignAppId_ReturnsFalse()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;
        ReplaceStoredChallengeAppId(harness.User, _foreignAppIdUrl);

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _foreignAppIdUrl);
        Assert.True(assertion.ClientExtensionResults.AppID);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", BuildWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyWithForeignAppIdInStoredChallenge_AssertionScopedToServerAppId_ReturnsTrue()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;
        ReplaceStoredChallengeAppId(harness.User, _foreignAppIdUrl);

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", BuildWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyAppIdScopedAssertionWithoutAppIdExtensionResult_ReturnsFalse()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        assertion.ClientExtensionResults.AppID = false;
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };

        var result = await harness.Provider.ValidateAsync("TwoFactor", BuildWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAsync_MigratedU2fKeyAssertionScopedToAppId_UpdatesStoredSignatureCounter()
    {
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var harness = CreateHarness(CreateMigratedU2fUser(authenticator));

        var optionsJson = await harness.Provider.GenerateAsync("TwoFactor", SubstituteUserManager(), harness.User);
        var challenge = AssertionOptions.FromJson(optionsJson).Challenge;

        var assertion = authenticator.MakeAssertion(challenge, _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);
        var userFromStorage = new User { TwoFactorProviders = harness.User.TwoFactorProviders };
        Assert.Equal(7u, LoadStoredKey(userFromStorage).SignatureCounter);

        var result = await harness.Provider.ValidateAsync("TwoFactor", BuildWebClientTokenString(assertion),
            SubstituteUserManager(), userFromStorage);

        Assert.True(result);
        Assert.Equal(authenticator.SignatureCounter, LoadStoredKey(userFromStorage).SignatureCounter);
        Assert.True(authenticator.SignatureCounter > 7u);
    }

    [Fact]
    public void WebClientTokenString_CarriesAppIdExtensionResultIntoAssertionResponse()
    {
        // Guards the round trip test: the extension result the web client sends under the "extensions" key
        // must reach the Fido2 model, otherwise the failure above could be blamed on a dropped result.
        using var authenticator = new FakeWebAuthnAuthenticator(LegacyU2fKeyHandle());
        var assertion = authenticator.MakeAssertion([1, 2, 3], _rpId, _vaultUrl, userHandle: null, appId: _u2fAppIdUrl);

        var parsed = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
            BuildWebClientTokenString(assertion), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

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
    /// A user whose WebAuthn provider holds one key in the shape the 2020 U2F-to-WebAuthn migration wrote it
    /// (util/Migrator/DbScripts/2020-09-09_00-ScriptMigrateU2FToWebAuthn.cs, TwoFactorProvider.U2fMetaData.ToWebAuthnData).
    /// UserHandle, CredType, RegDate and AaGuid were never set by the migration and keep their defaults.
    /// </summary>
    private static User CreateMigratedU2fUser(FakeWebAuthnAuthenticator authenticator)
    {
        const uint carriedOverU2fCounter = 7;
        authenticator.SignatureCounter = carriedOverU2fCounter;

        var key = new TwoFactorProvider.WebAuthnData
        {
            Name = "YubiKey 5 NFC",
            Descriptor = new PublicKeyCredentialDescriptor(authenticator.CredentialId),
            PublicKey = CreatePublicKeyFromU2fRegistrationData(authenticator.GetU2fRawPublicKey()),
            SignatureCounter = carriedOverU2fCounter,
            Migrated = true,
        };

        return CreateUserWithStoredKey(key);
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

    /// <summary>
    /// Port of the deleted <c>U2fMetaData.CreatePublicKeyFromU2fRegistrationData</c>: X is bytes 1..32 and Y is
    /// bytes 33..64 of the U2F public key, wrapped as COSE EC2 / ES256 / P-256. The original used PeterO.Cbor,
    /// which writes integer map keys in ascending numeric order (-3, -2, -1, 1, 3), reproduced here.
    /// </summary>
    private static byte[] CreatePublicKeyFromU2fRegistrationData(byte[] publicKeyData)
    {
        var x = new byte[32];
        var y = new byte[32];
        Buffer.BlockCopy(publicKeyData, 1, x, 0, 32);
        Buffer.BlockCopy(publicKeyData, 33, y, 0, 32);

        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(5);
        writer.WriteInt32(-3); writer.WriteByteString(y);
        writer.WriteInt32(-2); writer.WriteByteString(x);
        writer.WriteInt32(-1); writer.WriteInt32(1);
        writer.WriteInt32(1); writer.WriteInt32(2);
        writer.WriteInt32(3); writer.WriteInt32(-7);
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>
    /// A U2F key handle is an opaque, authenticator-chosen blob (64 bytes here). The leading bytes make the
    /// standard Base64 form contain '+' and '/', which is how Newtonsoft stored <c>Descriptor.Id</c>.
    /// </summary>
    private static byte[] LegacyU2fKeyHandle()
    {
        var keyHandle = new byte[64];
        for (var i = 0; i < keyHandle.Length; i++)
        {
            keyHandle[i] = (byte)(i * 7 + 3);
        }
        keyHandle[0] = 0xfb;
        keyHandle[1] = 0xff;
        return keyHandle;
    }

    /// <summary>
    /// Same JSON shape as <c>buildDataString</c> in clients/apps/web/src/connectors/common-webauthn.ts.
    /// The extension results travel under the "extensions" key, as the web client sends them.
    /// </summary>
    private static string BuildWebClientTokenString(AuthenticatorAssertionRawResponse assertion)
    {
        return JsonSerializer.Serialize(new
        {
            id = assertion.Id,
            rawId = Base64Url.EncodeToString(assertion.RawId),
            type = "public-key",
            extensions = assertion.ClientExtensionResults.AppID == true
                ? new Dictionary<string, object> { ["appid"] = true }
                : new Dictionary<string, object>(),
            response = new
            {
                authenticatorData = Base64Url.EncodeToString(assertion.Response.AuthenticatorData),
                clientDataJson = Base64Url.EncodeToString(assertion.Response.ClientDataJson),
                signature = Base64Url.EncodeToString(assertion.Response.Signature),
            },
        });
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
