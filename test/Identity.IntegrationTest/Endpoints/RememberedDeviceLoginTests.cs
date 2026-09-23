using System.Text;
using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
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
    /// The numeric value of <see cref="TwoFactorProviderType.Remember"/>. Sent as a number rather
    /// than the name because the validator-order selection parses it with <c>int.TryParse</c>.
    /// </summary>
    private const string _rememberProvider = "5";

    private const string _emailProvider = "1";

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
    /// H1 — a two-factor login that asks to be remembered gets a token back.
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
    /// H2 — the token is honored on a later login, with no second factor supplied. This is the
    /// behavior the whole feature exists to preserve; it must stay green through every change to the
    /// token format.
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
    /// H3 — the token is bound to the device it was issued to, so presenting it from another device
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
