using System.Text.Json;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Fakes;
using Bit.Test.Common.Helpers;
using static Bit.Identity.IntegrationTest.Helpers.TwoFactorLoginTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.TwoFactor;

public class TwoFactorLoginVerificationTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public TwoFactorLoginVerificationTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TokenEndpoint_GrantTypePassword_MigratedU2fKeyWebAuthnTwoFactor_AssertionScopedToAppId_Success(
        bool resavedAfterSuccessfulLogin)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        using var authenticator = new FakeWebAuthnAuthenticator(FakeWebAuthnAuthenticator.GetLegacyU2fKeyHandle());
        var twoFactorProviders = resavedAfterSuccessfulLogin
            ? authenticator.GetResavedMigratedU2fTwoFactorProvidersJson("YubiKey 5 NFC", FakeWebAuthnAuthenticator.CarriedOverU2fCounter)
            : authenticator.GetMigratedU2fTwoFactorProvidersJson("YubiKey 5 NFC", FakeWebAuthnAuthenticator.CarriedOverU2fCounter);
        await CreateUserAsync(localFactory, TestEmail, twoFactorProviders);
        var appId = CoreHelpers.U2fAppIdUrl(localFactory.GetService<GlobalSettings>());

        // Act: password login returns the WebAuthn challenge
        var challengeContext = await localFactory.ContextFromPasswordAsync(TestEmail, TestPassword);

        // Assert: the client is told to also use the server's U2F AppID
        var challengeBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(challengeContext);
        var providers = AssertHelper.AssertJsonProperty(challengeBody.RootElement, "TwoFactorProviders2", JsonValueKind.Object);
        Assert.True(providers.TryGetProperty("7", out var webAuthnOptions));
        Assert.Equal(appId, webAuthnOptions.GetProperty("extensions").GetProperty("appid").GetString());

        // Act: the authenticator signs for the AppID, as a browser does after the appid fallback
        var challenge = AssertionOptions.FromJson(webAuthnOptions.GetRawText()).Challenge;
        var assertion = authenticator.MakeAssertion(challenge, "localhost", "https://localhost:8080",
            userHandle: null, appId: appId);
        var loginContext = await localFactory.ContextFromPasswordWithTwoFactorAsync(
            TestEmail, TestPassword, twoFactorProviderType: "7", twoFactorToken: FakeWebAuthnAuthenticator.MakeWebClientTokenString(assertion));

        // Assert
        var loginBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(loginContext);
        var accessToken = AssertHelper.AssertJsonProperty(loginBody.RootElement, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(accessToken);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_UserTwoFactorRequired_TwoFactorProvided_Success()
    {
        // Arrange
        var factory = new IdentityApplicationFactory();

        // Create Test User
        await CreateUserAsync(factory, TestEmail, UserEmailTwoFactor);

        // Act
        var failedTokenContext = await factory.ContextFromPasswordAsync(TestEmail, TestPassword);

        Assert.Equal(StatusCodes.Status400BadRequest, failedTokenContext.Response.StatusCode);
        var emailToken = await EmailLoginCodeAsync(
            factory, TestEmail, IdentityApplicationFactory.DefaultDeviceIdentifier);

        var twoFactorProvidedContext = await factory.ContextFromPasswordWithTwoFactorAsync(
            TestEmail,
            TestPassword,
            twoFactorToken: emailToken);

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(twoFactorProvidedContext);
        var root = body.RootElement;

        var result = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_InvalidTwoFactorToken_Fails()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await CreateUserAsync(localFactory, TestEmail, UserEmailTwoFactor);

        // Act
        var context = await localFactory.ContextFromPasswordWithTwoFactorAsync(
                                TestEmail, TestPassword, twoFactorProviderType: "Email");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        var errorMessage = AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Two-step token is invalid. Try again.", errorMessage);

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("invalid_username_or_password", error);
    }

    /// <summary>
    /// A wrong email two-factor code is rejected, counts as a failed login, and emails the owner a failed-attempt
    /// notice.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_InvalidEmailTwoFactorCode_FailedLoginCountedAndOwnerNotified()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, BuildUserEmailTwoFactor(email));
        var userRepository = _factory.GetService<IUserRepository>();
        var failedLoginCountBefore = (await userRepository.GetByEmailAsync(email)).FailedLoginCount;

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Email), twoFactorToken: "000000");

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var errorModel = AssertHelper.AssertJsonProperty(body.RootElement, "ErrorModel", JsonValueKind.Object);
        Assert.Equal("Two-step token is invalid. Try again.",
            AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString());
        Assert.Equal(failedLoginCountBefore + 1, (await userRepository.GetByEmailAsync(email)).FailedLoginCount);
        await _factory.GetService<IMailService>().Received(1).SendFailedTwoFactorAttemptEmailAsync(
            email, TwoFactorProviderType.Email, Arg.Any<DateTime>(), Arg.Any<string>());
    }

    /// <summary>
    /// A token request without a device identifier fails two-factor even with the correct email code, and the code
    /// still logs in the device it was issued to.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_EmailCodeWithoutDeviceIdentifier_TwoFactorRejected()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, BuildUserEmailTwoFactor(email));
        var code = await EmailLoginCodeAsync(_factory, email, IdentityApplicationFactory.DefaultDeviceIdentifier);

        // Act
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "grant_type", "password" },
            { "username", email },
            { "password", TestPassword },
            { "TwoFactorToken", code },
            { "TwoFactorProvider", ProviderKey(TwoFactorProviderType.Email) },
        });
        var context = await _factory.Server.PostAsync("/connect/token", content);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var errorModel = AssertHelper.AssertJsonProperty(body.RootElement, "ErrorModel", JsonValueKind.Object);
        Assert.Equal("Two-step token is invalid. Try again.",
            AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString());
        var issuedDeviceContext = await _factory.ContextFromPasswordWithTwoFactorAsync(email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Email), twoFactorToken: code);
        using var issuedDeviceBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(issuedDeviceContext);
        AssertHelper.AssertJsonProperty(issuedDeviceBody.RootElement, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// A current authenticator app code completes two-factor login.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_ValidAuthenticatorCode_Success()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, UserAuthenticatorTwoFactor);
        var authenticatorCode = new Totp(Base32Encoding.ToBytes(AuthenticatorKey)).ComputeTotp();

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(email, TestPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Authenticator), twoFactorToken: authenticatorCode);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
    }
}
