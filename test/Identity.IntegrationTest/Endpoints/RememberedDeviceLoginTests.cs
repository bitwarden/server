using System.Text;
using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints;

/// <summary>
/// End-to-end coverage for the "remember this device" flow: a token issued by one login is honored
/// by the next, and is refused when it should not be.
/// </summary>
public class RememberedDeviceLoginTests
{
    private const string _testEmail = "test+rememberdevice@email.com";
    private const string _testPassword = "master_password_hash";
    private const string _emailToken = "12345678";
    private const string _userEmailTwoFactor =
        """{"1": { "Enabled": true, "MetaData": { "Email": "test+rememberdevice@email.com"}}}""";

    /// <summary>
    /// The numeric value of <see cref="TwoFactorProviderType.Remember"/>.
    /// </summary>
    private const string _rememberProvider = "5";

    private const string _emailProvider = "1";

    /// <summary>The numeric value of <see cref="TwoFactorProviderType.RecoveryCode"/>.</summary>
    private const string _recoveryCodeProvider = "8";

    private static async Task<(IdentityApplicationFactory Factory, User User)> CreateFactoryWithTwoFactorUserAsync()
    {
        var factory = new IdentityApplicationFactory();

        // The Email provider reads its one-time code from the distributed cache; pinning it lets the
        // test complete a real two-factor login.
        factory.SubstituteService<IDistributedCache>(distCache =>
        {
            distCache.GetAsync(Arg.Is<string>(s => s.StartsWith("EmailToken_")))
                .Returns(Task.FromResult(Encoding.UTF8.GetBytes(_emailToken)));
        });

        var user = await factory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = _testEmail,
                MasterPasswordHash = _testPassword,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel
                {
                    PublicKey = Bit.Test.Common.Constants.TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring
                },
                UserSymmetricKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            });

        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var userService = factory.GetService<IUserService>();
        user.TwoFactorProviders = _userEmailTwoFactor;
        await userRepository.ReplaceAsync(user);
        await userService.GetUserByIdAsync(user.Id);

        return (factory, user);
    }

    /// <summary>
    /// A two-factor login that asks to be remembered gets a token back.
    /// </summary>
    [Fact]
    public async Task RememberRequested_ResponseCarriesRememberToken()
    {
        var (factory, _) = await CreateFactoryWithTwoFactorUserAsync();

        var (accessToken, rememberToken) = await factory.TokensFromPasswordWithTwoFactorAsync(
            _testEmail, _testPassword, twoFactorProviderType: _emailProvider, twoFactorToken: _emailToken);

        Assert.NotNull(accessToken);
        Assert.False(string.IsNullOrEmpty(rememberToken));
    }

    /// <summary>
    /// The token is honored on a later login, with no second factor supplied.
    /// </summary>
    [Fact]
    public async Task RememberToken_ReplayedOnNextLogin_SkipsTwoFactorChallenge()
    {
        var (factory, _) = await CreateFactoryWithTwoFactorUserAsync();

        var (_, rememberToken) = await factory.TokensFromPasswordWithTwoFactorAsync(
            _testEmail, _testPassword, twoFactorProviderType: _emailProvider, twoFactorToken: _emailToken);
        Assert.False(string.IsNullOrEmpty(rememberToken));

        // Clients send remember=0 when re-authenticating with a stored token.
        var context = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            twoFactorProviderType: _rememberProvider,
            twoFactorToken: rememberToken!,
            twoFactorRemember: "0");

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var accessToken = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));
    }

    /// <summary>
    /// A login that presents a remember token must not be issued another one, or an active user's
    /// remember-me would renew forever. Replayed with and without asking to be remembered again, since
    /// a client can send <c>TwoFactorRemember=1</c> regardless.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public async Task RememberToken_ReplayedOnNextLogin_DoesNotIssueAnotherToken(string twoFactorRemember)
    {
        var (factory, _) = await CreateFactoryWithTwoFactorUserAsync();

        var (_, rememberToken) = await factory.TokensFromPasswordWithTwoFactorAsync(
            _testEmail, _testPassword, twoFactorProviderType: _emailProvider, twoFactorToken: _emailToken);
        Assert.False(string.IsNullOrEmpty(rememberToken));

        var context = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            twoFactorProviderType: _rememberProvider,
            twoFactorToken: rememberToken!,
            twoFactorRemember: twoFactorRemember);

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        Assert.False(root.TryGetProperty("TwoFactorToken", out _));
    }

    [Fact]
    public async Task LegacyRememberToken_IsStillAccepted()
    {
        var (factory, user) = await CreateFactoryWithTwoFactorUserAsync();
        var legacyToken = await factory.GenerateLegacyRememberTokenAsync(user);

        var context = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            twoFactorProviderType: _rememberProvider,
            twoFactorToken: legacyToken,
            twoFactorRemember: "0");

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// Accepting a legacy token also replaces it, so each device moves to the current format on
    /// its next login rather than waiting for its token to expire.
    /// </summary>
    [Fact]
    public async Task LegacyRememberToken_IsUpgradedToCurrentFormat()
    {
        var (factory, user) = await CreateFactoryWithTwoFactorUserAsync();
        var legacyToken = await factory.GenerateLegacyRememberTokenAsync(user);

        var context = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            twoFactorProviderType: _rememberProvider,
            twoFactorToken: legacyToken,
            twoFactorRemember: "0");

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var upgraded = AssertHelper.AssertJsonProperty(root, "TwoFactorToken", JsonValueKind.String).GetString();
        Assert.StartsWith(TwoFactorRememberTokenable.ClearTextPrefix, upgraded);

        // And the replacement works on the login after that.
        var replayContext = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            twoFactorProviderType: _rememberProvider,
            twoFactorToken: upgraded!,
            twoFactorRemember: "0");

        using var replayBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(replayContext);
        AssertHelper.AssertJsonProperty(replayBody.RootElement, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// Using a recovery code tears two-factor down, and the devices trusted under the old
    /// configuration stop being trusted with it.
    /// </summary>
    /// <remarks>
    /// The replay happens <em>after</em> two-factor is re-enabled, deliberately. While the account
    /// has no second factor, no challenge is issued at all and the replay would log in on the
    /// password alone, so it would say nothing about whether the revocation worked.
    /// <para>
    /// The recovery login is sent with and without asking to be remembered. Either way it must not
    /// come back with a new remember token, which would undo the revocation for that device.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public async Task RecoveryCodeTeardown_RevokesRememberedDevices(string twoFactorRemember)
    {
        var (factory, user) = await CreateFactoryWithTwoFactorUserAsync();
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();

        const string recoveryCode = "recoverycode123";
        user.TwoFactorRecoveryCode = recoveryCode;
        await userRepository.ReplaceAsync(user);

        var (_, rememberToken) = await factory.TokensFromPasswordWithTwoFactorAsync(
            _testEmail, _testPassword, twoFactorProviderType: _emailProvider, twoFactorToken: _emailToken);
        Assert.False(string.IsNullOrEmpty(rememberToken));

        // Tear down two-factor with the recovery code. The provider is sent as its numeric value because
        // BaseRequestValidator.DetermineValidationOrder only recognises a recovery-code request via int.TryParse.
        var teardown = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            twoFactorProviderType: _recoveryCodeProvider,
            twoFactorToken: recoveryCode,
            twoFactorRemember: twoFactorRemember);
        using (var teardownBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(teardown))
        {
            AssertHelper.AssertJsonProperty(teardownBody.RootElement, "access_token", JsonValueKind.String);
            Assert.False(teardownBody.RootElement.TryGetProperty("TwoFactorToken", out _));
        }

        // Put a second factor back, so the replay below is decided by the remembered-device state
        // rather than by the account simply having no second factor.
        var recoveredUser = await userRepository.GetByEmailAsync(_testEmail);
        Assert.NotNull(recoveredUser);
        recoveredUser.TwoFactorProviders = _userEmailTwoFactor;
        await userRepository.ReplaceAsync(recoveredUser);

        var replay = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            twoFactorProviderType: _rememberProvider,
            twoFactorToken: rememberToken!,
            twoFactorRemember: "0");

        using var replayBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(replay);
        var root = replayBody.RootElement;

        Assert.False(root.TryGetProperty("access_token", out _));
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    /// <summary>
    /// The token is bound to the device it was issued to, so presenting it from another device
    /// falls back to a two-factor challenge.
    /// </summary>
    [Fact]
    public async Task RememberToken_PresentedFromDifferentDevice_IsRefused()
    {
        var (factory, _) = await CreateFactoryWithTwoFactorUserAsync();

        var (_, rememberToken) = await factory.TokensFromPasswordWithTwoFactorAsync(
            _testEmail, _testPassword, twoFactorProviderType: _emailProvider, twoFactorToken: _emailToken);
        Assert.False(string.IsNullOrEmpty(rememberToken));

        var context = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
            deviceIdentifier: Guid.NewGuid().ToString(),
            twoFactorProviderType: _rememberProvider,
            twoFactorToken: rememberToken!,
            twoFactorRemember: "0");

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        // A refused remember token falls back to a challenge rather than a generic auth failure:
        // the password was fine, the device just is not trusted.
        Assert.False(root.TryGetProperty("access_token", out _));
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
        AssertHelper.AssertJsonProperty(root, "TwoFactorProviders2", JsonValueKind.Object);
    }
}
