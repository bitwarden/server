using System.Text.Json;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using static Bit.Identity.IntegrationTest.Helpers.TwoFactorLoginTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.TwoFactor;

public class TwoFactorLoginChallengeTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public TwoFactorLoginChallengeTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_UserTwoFactorRequired_NoTwoFactorProvided_Fails()
    {
        // Arrange
        await CreateUserAsync(_factory, TestEmail, UserEmailTwoFactor);

        // Act
        var context = await _factory.ContextFromPasswordAsync(TestEmail, TestPassword);

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_UserWebAuthnTwoFactorRequired_CredentialIdIsStandardBase64WithPlusAndSlash_ListsProvider()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await CreateUserAsync(localFactory, TestEmail, UserWebAuthnTwoFactor);

        // Act
        var context = await localFactory.ContextFromPasswordAsync(TestEmail, TestPassword);

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        // Getting this far - rather than a 500 from an unhandled JsonException while building the
        // WebAuthn challenge - is the whole point: it proves the stored credential decoded successfully.
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);

        var providers = AssertHelper.AssertJsonProperty(root, "TwoFactorProviders2", JsonValueKind.Object);
        Assert.True(providers.TryGetProperty("7", out _));
    }

    /// <summary>
    /// The email two-factor challenge returns the redacted two-factor email, the account email, and the session
    /// token SSO clients use to request the emailed code.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_EmailTwoFactorRequired_ChallengeIncludesRedactedEmailAndSessionToken()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, BuildUserEmailTwoFactor(email));

        // Act
        var context = await _factory.ContextFromPasswordAsync(email, TestPassword);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;
        var providers = AssertHelper.AssertJsonProperty(root, "TwoFactorProviders2", JsonValueKind.Object);
        var emailProvider = AssertHelper.AssertJsonProperty(
            providers, ProviderKey(TwoFactorProviderType.Email), JsonValueKind.Object);
        Assert.Equal(CoreHelpers.RedactEmailAddress(email),
            AssertHelper.AssertJsonProperty(emailProvider, "Email", JsonValueKind.String).GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            AssertHelper.AssertJsonProperty(root, "SsoEmail2faSessionToken", JsonValueKind.String).GetString()));
        Assert.Equal(email, AssertHelper.AssertJsonProperty(root, "Email", JsonValueKind.String).GetString());
    }

    /// <summary>
    /// A disabled email provider does not count as two-factor, so the user logs in without a challenge.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_EmailTwoFactorDisabled_NoTwoFactorChallenge()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserWithStoredTwoFactorProvidersAsync(email,
            "{\"1\":{\"Enabled\":false,\"MetaData\":{\"Email\":\"" + email + "\"}}}");

        // Act
        var context = await _factory.ContextFromPasswordAsync(email, TestPassword);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.True(body.RootElement.TryGetProperty("access_token", out _), body.RootElement.ToString());
    }

    /// <summary>
    /// The two-factor challenge issues no email code, in either the current or the previous cache format. The
    /// client requests the code separately, and only then is one stored and emailed.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_EmailTwoFactorRequired_ChallengeIssuesNoEmailCode()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, BuildUserEmailTwoFactor(email));
        var user = await _factory.GetService<IUserRepository>().GetByEmailAsync(email);
        var cache = _factory.Services.GetRequiredKeyedService<IDistributedCache>("persistent");

        // Act
        var context = await _factory.ContextFromPasswordAsync(email, TestPassword);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal("Two factor required.",
            AssertHelper.AssertJsonProperty(body.RootElement, "error_description", JsonValueKind.String).GetString());
        Assert.Null(await cache.GetAsync($"EmailToken_{user.Id}_{user.SecurityStamp}_TwoFactor"));
        Assert.Null(await cache.GetAsync($"TwoFactorEmail_LoginCode_{user.Id}_{user.SecurityStamp}"));
        Assert.False(_factory.TwoFactorEmailCodes.ContainsKey(email));
    }

    /// <summary>
    /// A user with both email and WebAuthn two-factor gets a challenge for each, including the WebAuthn
    /// assertion options.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_EmailAndWebAuthnTwoFactorRequired_ChallengeIncludesBothProviders()
    {
        // Arrange
        var email = NewUniqueEmail();
        var emailAndWebAuthnTwoFactor = BuildUserEmailTwoFactor(email)[..^1] + "," + UserWebAuthnTwoFactor[1..];
        await CreateUserAsync(_factory, email, emailAndWebAuthnTwoFactor);
        var userRepository = _factory.GetService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(email);
        user.Premium = true;
        await userRepository.ReplaceAsync(user);

        // Act
        var context = await _factory.ContextFromPasswordAsync(email, TestPassword);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var providers = AssertHelper.AssertJsonProperty(body.RootElement, "TwoFactorProviders2", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(providers, ProviderKey(TwoFactorProviderType.Email), JsonValueKind.Object);
        var webAuthn = AssertHelper.AssertJsonProperty(
            providers, ProviderKey(TwoFactorProviderType.WebAuthn), JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(webAuthn, "challenge", JsonValueKind.String);
    }

    /// <summary>
    /// Creates a user and stores the two-factor provider JSON exactly as given. <see cref="CreateUserAsync"/> saves
    /// providers through the user service, which marks the provider enabled.
    /// </summary>
    private async Task CreateUserWithStoredTwoFactorProvidersAsync(string email, string twoFactorProvidersJson)
    {
        await CreateUserAsync(_factory, email);
        var userRepository = _factory.GetService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(email);
        user.TwoFactorProviders = twoFactorProvidersJson;
        await userRepository.ReplaceAsync(user);
    }
}
