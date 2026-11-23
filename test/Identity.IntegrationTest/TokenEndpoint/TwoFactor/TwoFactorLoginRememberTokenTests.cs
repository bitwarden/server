using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using OtpNet;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TwoFactorLoginTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.TwoFactor;

/// <summary>
/// End-to-end coverage for 2FA remember tokens: a token issued by one login is honored by the next,
/// and is refused when it should not be.
/// </summary>
public class TwoFactorLoginRememberTokenTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public TwoFactorLoginRememberTokenTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Registers a user with email two-factor under a unique address, so tests can share the class fixture's
    /// host instead of each starting (and leaving running) a host of their own.
    /// </summary>
    private async Task<(string Email, User User)> CreateTwoFactorUserAsync()
    {
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, BuildUserEmailTwoFactor(email));
        var user = await _factory.GetService<IUserRepository>().GetByEmailAsync(email);
        return (email, user);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_RememberTwoFactorType_InvalidTwoFactorToken_Fails()
    {
        // Arrange
        var (email, _) = await CreateTwoFactorUserAsync();

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(
                                email, TestPassword, twoFactorProviderType: "Remember");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    /// <summary>
    /// The 2FA remember token returned by an earlier two-factor login completes a later login on its own.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_ValidRememberToken_Success()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, UserAuthenticatorTwoFactor);
        var authenticatorCode = new Totp(Base32Encoding.ToBytes(AuthenticatorKey)).ComputeTotp();
        var firstLogin = await _factory.ContextFromPasswordWithTwoFactorAsync(email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Authenticator), twoFactorToken: authenticatorCode);
        using var firstLoginBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(firstLogin);
        var rememberToken = AssertHelper.AssertJsonProperty(
            firstLoginBody.RootElement, "TwoFactorToken", JsonValueKind.String).GetString();

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Remember), twoFactorToken: rememberToken);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// A login that presents a 2FA remember token completes on the token alone, with no second factor
    /// supplied, and must not be issued another one, or an active user's 2FA remember token would renew
    /// forever. Replayed with and without asking for another 2FA remember token, since a client can send
    /// <c>TwoFactorRemember=1</c> regardless. Clients send <c>0</c> when re-authenticating with a stored token.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public async Task RememberToken_ReplayedOnNextLogin_DoesNotIssueAnotherToken(string twoFactorRemember)
    {
        var (email, _) = await CreateTwoFactorUserAsync();

        var emailCode = await EmailLoginCodeAsync(
            _factory, email, IdentityApplicationFactory.DefaultDeviceIdentifier);
        var (_, rememberToken) = await _factory.TokensFromPasswordWithTwoFactorAsync(
            email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Email), twoFactorToken: emailCode);
        Assert.False(string.IsNullOrEmpty(rememberToken));

        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(
            email,
            TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Remember),
            twoFactorToken: rememberToken!,
            twoFactorRemember: twoFactorRemember);

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        Assert.False(root.TryGetProperty("TwoFactorToken", out _));
    }

    /// <summary>
    /// A legacy token is still accepted, and accepting it also replaces it, so each device moves to the
    /// current format on its next login rather than waiting for its token to expire.
    /// </summary>
    [Fact]
    public async Task LegacyRememberToken_IsUpgradedToCurrentFormat()
    {
        var (email, user) = await CreateTwoFactorUserAsync();
        var legacyToken = await _factory.GenerateLegacyRememberTokenAsync(user);

        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(
            email,
            TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Remember),
            twoFactorToken: legacyToken,
            twoFactorRemember: "0");

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var upgraded = AssertHelper.AssertJsonProperty(root, "TwoFactorToken", JsonValueKind.String).GetString();
        Assert.StartsWith(TwoFactorRememberTokenable.ClearTextPrefix, upgraded);

        // And the replacement works on the login after that.
        var replayContext = await _factory.ContextFromPasswordWithTwoFactorAsync(
            email,
            TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Remember),
            twoFactorToken: upgraded!,
            twoFactorRemember: "0");

        using var replayBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(replayContext);
        AssertHelper.AssertJsonProperty(replayBody.RootElement, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// Using a recovery code tears two-factor down, and the user's 2FA remember tokens
    /// stop being honored with it.
    /// </summary>
    /// <remarks>
    /// The replay happens <em>after</em> two-factor is re-enabled, deliberately. While the account
    /// has no second factor, no challenge is issued at all and the replay would log in on the
    /// password alone, so it would say nothing about whether the revocation worked.
    /// <para>
    /// The recovery login is sent with and without asking for a 2FA remember token. Either way it must not
    /// come back with a new 2FA remember token, which would undo the revocation for that device.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public async Task RecoveryCodeTeardown_RevokesTwoFactorRememberTokens(string twoFactorRemember)
    {
        var (email, user) = await CreateTwoFactorUserAsync();
        var userRepository = _factory.Services.GetRequiredService<IUserRepository>();

        const string recoveryCode = "recoverycode123";
        user.TwoFactorRecoveryCode = recoveryCode;
        await userRepository.ReplaceAsync(user);

        var emailCode = await EmailLoginCodeAsync(
            _factory, email, IdentityApplicationFactory.DefaultDeviceIdentifier);
        var (_, rememberToken) = await _factory.TokensFromPasswordWithTwoFactorAsync(
            email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Email), twoFactorToken: emailCode);
        Assert.False(string.IsNullOrEmpty(rememberToken));

        // Tear down two-factor with the recovery code. The provider is sent as its numeric value because
        // BaseRequestValidator.DetermineValidationOrder only recognises a recovery-code request via int.TryParse.
        var teardown = await _factory.ContextFromPasswordWithTwoFactorAsync(
            email,
            TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.RecoveryCode),
            twoFactorToken: recoveryCode,
            twoFactorRemember: twoFactorRemember);
        using (var teardownBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(teardown))
        {
            AssertHelper.AssertJsonProperty(teardownBody.RootElement, "access_token", JsonValueKind.String);
            Assert.False(teardownBody.RootElement.TryGetProperty("TwoFactorToken", out _));
        }

        // Put a second factor back, so the replay below is decided by the 2FA remember token state
        // rather than by the account simply having no second factor.
        var recoveredUser = await userRepository.GetByEmailAsync(email);
        Assert.NotNull(recoveredUser);
        recoveredUser.TwoFactorProviders = BuildUserEmailTwoFactor(email);
        await userRepository.ReplaceAsync(recoveredUser);

        var replay = await _factory.ContextFromPasswordWithTwoFactorAsync(
            email,
            TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Remember),
            twoFactorToken: rememberToken!,
            twoFactorRemember: "0");

        using var replayBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(replay);
        var root = replayBody.RootElement;

        Assert.False(root.TryGetProperty("access_token", out _));
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    /// <summary>
    /// A wrong recovery code must not tear anything down. Revocation only happens once the code is verified,
    /// so a failed attempt leaves the user's 2FA remember tokens honored. Otherwise anyone who knows the
    /// password could cut off every device with a 2FA remember token by guessing.
    /// </summary>
    [Fact]
    public async Task WrongRecoveryCode_DoesNotRevokeTwoFactorRememberTokens()
    {
        var (email, user) = await CreateTwoFactorUserAsync();
        var userRepository = _factory.Services.GetRequiredService<IUserRepository>();

        user.TwoFactorRecoveryCode = "recoverycode123";
        await userRepository.ReplaceAsync(user);

        var emailCode = await EmailLoginCodeAsync(
            _factory, email, IdentityApplicationFactory.DefaultDeviceIdentifier);
        var (_, rememberToken) = await _factory.TokensFromPasswordWithTwoFactorAsync(
            email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Email), twoFactorToken: emailCode);
        Assert.False(string.IsNullOrEmpty(rememberToken));

        var device = await _factory.GetService<IDeviceRepository>()
            .GetByIdentifierAsync(IdentityApplicationFactory.DefaultDeviceIdentifier, user.Id);
        Assert.NotNull(device);
        var rememberTokenRepository = _factory.GetService<ITwoFactorRememberTokenRepository>();
        var rowBefore = await rememberTokenRepository.GetByUserIdDeviceIdAsync(user.Id, device.Id);
        Assert.NotNull(rowBefore);

        // Attempt recovery with the wrong code. It must be refused.
        var wrongCode = await _factory.ContextFromPasswordWithTwoFactorAsync(
            email,
            TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.RecoveryCode),
            twoFactorToken: "not-the-recovery-code",
            twoFactorRemember: "0");
        using (var wrongCodeBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(wrongCode))
        {
            Assert.False(wrongCodeBody.RootElement.TryGetProperty("access_token", out _));
        }

        // The row's stamp is untouched...
        var rowAfter = await rememberTokenRepository.GetByUserIdDeviceIdAsync(user.Id, device.Id);
        Assert.NotNull(rowAfter);
        Assert.Equal(rowBefore.Stamp, rowAfter.Stamp);

        // ...so the old 2FA remember token still skips the challenge.
        var replay = await _factory.ContextFromPasswordWithTwoFactorAsync(
            email,
            TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Remember),
            twoFactorToken: rememberToken!,
            twoFactorRemember: "0");
        using var replayBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(replay);
        AssertHelper.AssertJsonProperty(replayBody.RootElement, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// The token is bound to the device it was issued to, so presenting it from another device
    /// falls back to a two-factor challenge.
    /// </summary>
    [Fact]
    public async Task RememberToken_PresentedFromDifferentDevice_IsRefused()
    {
        var (email, _) = await CreateTwoFactorUserAsync();

        var emailCode = await EmailLoginCodeAsync(
            _factory, email, IdentityApplicationFactory.DefaultDeviceIdentifier);
        var (_, rememberToken) = await _factory.TokensFromPasswordWithTwoFactorAsync(
            email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Email), twoFactorToken: emailCode);
        Assert.False(string.IsNullOrEmpty(rememberToken));

        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(
            email,
            TestPassword,
            deviceIdentifier: Guid.NewGuid().ToString(),
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Remember),
            twoFactorToken: rememberToken!,
            twoFactorRemember: "0");

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        // A refused 2FA remember token falls back to a challenge rather than a generic auth failure:
        // the password was fine, the 2FA remember token is just no longer honored.
        Assert.False(root.TryGetProperty("access_token", out _));
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
        AssertHelper.AssertJsonProperty(root, "TwoFactorProviders2", JsonValueKind.Object);
    }
}
