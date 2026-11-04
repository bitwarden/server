using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using OtpNet;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TwoFactorLoginTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.TwoFactor;

public class TwoFactorLoginRememberTokenTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public TwoFactorLoginRememberTokenTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_RememberTwoFactorType_InvalidTwoFactorToken_Fails()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await CreateUserAsync(localFactory, TestEmail, UserEmailTwoFactor);

        // Act
        var context = await localFactory.ContextFromPasswordWithTwoFactorAsync(
                                TestEmail, TestPassword, twoFactorProviderType: "Remember");

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
}
