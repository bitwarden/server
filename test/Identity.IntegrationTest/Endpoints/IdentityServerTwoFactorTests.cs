using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon.Factories;
using Bit.IntegrationTestCommon.Fido2;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using LinqToDB;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using OtpNet;
using Xunit;

// #nullable enable

namespace Bit.Identity.IntegrationTest.Endpoints;

public class IdentityServerTwoFactorTests : IClassFixture<IdentityApplicationFactory>
{
    const string _organizationTwoFactor = """{"6":{"Enabled":true,"MetaData":{"ClientId":"DIEFB13LB49IEB3459N2","ClientSecret":"0ZnsZHav0KcNPBZTS6EOUwqLPoB0sfMd5aJeWExQ","Host":"api-example.duosecurity.com"}}}""";
    const string _testEmail = "test+2farequired@email.com";
    const string _testPassword = "master_password_hash";
    const string _authenticatorKey = "JBSWY3DPEHPK3PXP";
    const string _userAuthenticatorTwoFactor =
        $$"""{"0": { "Enabled": true, "MetaData": { "Key": "{{_authenticatorKey}}" } } }""";
    const string _userEmailTwoFactor = """{"1": { "Enabled": true, "MetaData": { "Email": "test+2farequired@email.com"}}}""";

    // WebAuthn keys are persisted through JsonHelpers.LegacySerialize (Newtonsoft), which writes
    // Descriptor.Id as standard Base64, not Fido2NetLib's Base64Url - this Id contains both '+'
    // and '/'. Building the login challenge for this provider (WebAuthnTokenProvider.GenerateAsync
    // -> LoadKeys) decodes it through Fido2's Base64UrlConverter, which v4 tightened to reject
    // those characters unless relaxed decoding is enabled.
    private static readonly string _userWebAuthnTwoFactor = BuildUserWebAuthnTwoFactorJson();

    private static string BuildUserWebAuthnTwoFactorJson()
    {
        // PublicKey/UserHandle are never cryptographically validated in this flow (no assertion is
        // verified until the client responds to the challenge) - only their shape matters. Use a
        // real COSE_Key CBOR-encoded ECDSA P-256 public key (what Fido2NetLib actually stores)
        // instead of a placeholder, so the fixture matches production data.
        using var authenticator = new FakeWebAuthnAuthenticator();
        var publicKey = Convert.ToBase64String(authenticator.GetCosePublicKey());
        var userHandle = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        return "{\"7\":{\"Enabled\":true,\"MetaData\":{\"Key0\":{\"Name\":\"YubiKey\",\"Descriptor\":{\"Id\":\"RtCGgkCX5KOVz/9GaZxzxKHNEDQTW06jb4SlSt96DqA=\",\"Type\":0,\"Transports\":null},"
            + "\"PublicKey\":\"" + publicKey + "\",\"UserHandle\":\"" + userHandle + "\","
            + "\"SignatureCounter\":0,\"RegDate\":\"2024-01-01T00:00:00\",\"Migrated\":false,\"AaGuid\":\"00000000-0000-0000-0000-000000000000\"}}}}";
    }

    private readonly IdentityApplicationFactory _factory;

    public IdentityServerTwoFactorTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_UserTwoFactorRequired_NoTwoFactorProvided_Fails()
    {
        // Arrange
        await CreateUserAsync(_factory, _testEmail, _userEmailTwoFactor);

        // Act
        var context = await _factory.ContextFromPasswordAsync(_testEmail, _testPassword);

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
        await CreateUserAsync(localFactory, _testEmail, _userWebAuthnTwoFactor);

        // Act
        var context = await localFactory.ContextFromPasswordAsync(_testEmail, _testPassword);

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

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_UserTwoFactorRequired_TwoFactorProvided_Success()
    {
        // Arrange
        var factory = new IdentityApplicationFactory();

        // Create Test User
        await CreateUserAsync(factory, _testEmail, _userEmailTwoFactor);

        // Act
        var failedTokenContext = await factory.ContextFromPasswordAsync(_testEmail, _testPassword);

        Assert.Equal(StatusCodes.Status400BadRequest, failedTokenContext.Response.StatusCode);
        var emailToken = await EmailLoginCodeAsync(
            factory, _testEmail, IdentityApplicationFactory.DefaultDeviceIdentifier);

        var twoFactorProvidedContext = await factory.ContextFromPasswordWithTwoFactorAsync(
            _testEmail,
            _testPassword,
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
        await CreateUserAsync(localFactory, _testEmail, _userEmailTwoFactor);

        // Act
        var context = await localFactory.ContextFromPasswordWithTwoFactorAsync(
                                _testEmail, _testPassword, twoFactorProviderType: "Email");

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
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(email, _testPassword,
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

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypePassword_OrgDuoTwoFactorRequired_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, _testEmail, orgTwoFactor: _organizationTwoFactor);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "password" },
            { "username", _testEmail },
            { "password", _testPassword },
        }));

        // Assert
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_RememberTwoFactorType_InvalidTwoFactorToken_Fails()
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await CreateUserAsync(localFactory, _testEmail, _userEmailTwoFactor);

        // Act
        var context = await localFactory.ContextFromPasswordWithTwoFactorAsync(
                                _testEmail, _testPassword, twoFactorProviderType: "Remember");

        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredential_OrgTwoFactorRequired_Success(Organization organization, OrganizationApiKey organizationApiKey)
    {
        // Arrange
        organization.Enabled = true;
        organization.UseApi = true;
        organization.Use2fa = true;
        organization.TwoFactorProviders = _organizationTwoFactor;

        var orgRepo = _factory.Services.GetRequiredService<IOrganizationRepository>();
        organization = await orgRepo.CreateAsync(organization);

        organizationApiKey.OrganizationId = organization.Id;
        organizationApiKey.Type = OrganizationApiKeyType.Default;

        var orgApiKeyRepo = _factory.Services.GetRequiredService<IOrganizationApiKeyRepository>();
        await orgApiKeyRepo.CreateAsync(organizationApiKey);

        // Act
        var context = await _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"organization.{organization.Id}" },
            { "client_secret", organizationApiKey.ApiKey },
            { "scope", "api.organization" },
        }));

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;
        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(token);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeClientCredential_IndvTwoFactorRequired_Success(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        await CreateUserAsync(localFactory, _testEmail, _userEmailTwoFactor);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.FirstAsync(u => u.Email == _testEmail);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", $"user.{user.Id}" },
            { "client_secret", user.ApiKey },
            { "scope", "api" },
            { "DeviceIdentifier", deviceId },
            { "DeviceType",  ((int)DeviceType.FirefoxBrowser).ToString() },
            { "DeviceName", "firefox" },
        }));

        // Assert
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;
        var token = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(token);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_IndvTwoFactor_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, _testEmail, userTwoFactor: _userEmailTwoFactor);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Assert
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_IndvTwoFactor_TwoFactorProvided_Success(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();

        // Create Test User
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };
        await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, _testEmail, userTwoFactor: _userEmailTwoFactor);

        // Act
        var failedTokenContext = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        Assert.Equal(StatusCodes.Status400BadRequest, failedTokenContext.Response.StatusCode);
        var emailToken = await EmailLoginCodeAsync(localFactory, _testEmail, deviceId);

        var twoFactorProvidedContext = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "twoFactorToken", emailToken},
            { "twoFactorProvider", "1" },
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));


        // Assert
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(twoFactorProvidedContext);
        var root = body.RootElement;

        var result = AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String).GetString();
        Assert.NotNull(result);
    }

    [Theory, BitAutoData]
    public async Task TokenEndpoint_GrantTypeAuthCode_OrgTwoFactorRequired_OrgDuoTwoFactor_NoTwoFactorProvided_Fails(string deviceId)
    {
        // Arrange
        var localFactory = new IdentityApplicationFactory();
        var challenge = new string('c', 50);
        var ssoConfigData = new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.MasterPassword,
        };

        await CreateSsoOrganizationAndUserAsync(
            localFactory, ssoConfigData, challenge, _testEmail, orgTwoFactor: _organizationTwoFactor);

        // Act
        var context = await localFactory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "12" },
            { "deviceIdentifier", deviceId },
            { "deviceName", "edge" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Assert
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        var error = AssertHelper.AssertJsonProperty(root, "error_description", JsonValueKind.String).GetString();
        Assert.Equal("Two factor required.", error);
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
        var context = await _factory.ContextFromPasswordAsync(email, _testPassword);

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
        var context = await _factory.ContextFromPasswordAsync(email, _testPassword);

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
        var context = await _factory.ContextFromPasswordAsync(email, _testPassword);

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
        var emailAndWebAuthnTwoFactor = BuildUserEmailTwoFactor(email)[..^1] + "," + _userWebAuthnTwoFactor[1..];
        await CreateUserAsync(_factory, email, emailAndWebAuthnTwoFactor);
        var userRepository = _factory.GetService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(email);
        user.Premium = true;
        await userRepository.ReplaceAsync(user);

        // Act
        var context = await _factory.ContextFromPasswordAsync(email, _testPassword);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var providers = AssertHelper.AssertJsonProperty(body.RootElement, "TwoFactorProviders2", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(providers, ProviderKey(TwoFactorProviderType.Email), JsonValueKind.Object);
        var webAuthn = AssertHelper.AssertJsonProperty(
            providers, ProviderKey(TwoFactorProviderType.WebAuthn), JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(webAuthn, "challenge", JsonValueKind.String);
    }

    /// <summary>
    /// A current authenticator app code completes two-factor login.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_ValidAuthenticatorCode_Success()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, _userAuthenticatorTwoFactor);
        var authenticatorCode = new Totp(Base32Encoding.ToBytes(_authenticatorKey)).ComputeTotp();

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(email, _testPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Authenticator), twoFactorToken: authenticatorCode);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// The recovery code completes login and removes the user's two-factor providers.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_ValidRecoveryCode_SuccessAndTwoFactorRemoved()
    {
        // Arrange
        const string recoveryCode = "abcdefghijklmnopqrstuvwxyz012345";
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, BuildUserEmailTwoFactor(email));
        var userRepository = _factory.GetService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(email);
        user.TwoFactorRecoveryCode = recoveryCode;
        await userRepository.ReplaceAsync(user);

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(email, _testPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.RecoveryCode), twoFactorToken: recoveryCode);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
        Assert.Null((await userRepository.GetByEmailAsync(email)).TwoFactorProviders);
    }

    /// <summary>
    /// The remember-me token returned by an earlier two-factor login completes a later login on its own.
    /// </summary>
    [Fact]
    public async Task TokenEndpoint_GrantTypePassword_ValidRememberToken_Success()
    {
        // Arrange
        var email = NewUniqueEmail();
        await CreateUserAsync(_factory, email, _userAuthenticatorTwoFactor);
        var authenticatorCode = new Totp(Base32Encoding.ToBytes(_authenticatorKey)).ComputeTotp();
        var firstLogin = await _factory.ContextFromPasswordWithTwoFactorAsync(email, _testPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Authenticator), twoFactorToken: authenticatorCode);
        using var firstLoginBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(firstLogin);
        var rememberToken = AssertHelper.AssertJsonProperty(
            firstLoginBody.RootElement, "TwoFactorToken", JsonValueKind.String).GetString();

        // Act
        var context = await _factory.ContextFromPasswordWithTwoFactorAsync(email, _testPassword,
            twoFactorProviderType: ProviderKey(TwoFactorProviderType.Remember), twoFactorToken: rememberToken);

        // Assert
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// Emails a real login code for the given device through the Identity host's email service and returns it.
    /// </summary>
    private static async Task<string> EmailLoginCodeAsync(
        IdentityApplicationFactory factory, string email, string deviceIdentifier)
    {
        var user = await factory.GetService<IUserRepository>().GetByEmailAsync(email);
        await factory.GetService<ITwoFactorEmailService>().SendTwoFactorLoginEmailAsync(user, deviceIdentifier);
        return factory.TwoFactorEmailCodes[email];
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

    private static string NewUniqueEmail() => $"two-factor-{Guid.NewGuid()}@bitwarden.com";

    private static string BuildUserEmailTwoFactor(string email) =>
        "{\"1\":{\"Enabled\":true,\"MetaData\":{\"Email\":\"" + email + "\"}}}";

    private static string ProviderKey(TwoFactorProviderType providerType) =>
        ((int)providerType).ToString(CultureInfo.InvariantCulture);

    private async Task CreateUserAsync(
        IdentityApplicationFactory factory,
        string testEmail,
        string userTwoFactor = null)
    {
        // Create Test User
        var user = await factory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = testEmail,
                MasterPasswordHash = _testPassword,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel()
                {
                    PublicKey = Bit.Test.Common.Constants.TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring
                },
                UserSymmetricKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            });
        Assert.NotNull(user);

        var userService = factory.GetService<IUserService>();
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        if (userTwoFactor != null)
        {
            user.TwoFactorProviders = userTwoFactor;
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
            user = await userRepository.GetByEmailAsync(testEmail);
            Assert.NotNull(user.TwoFactorProviders);
        }
    }

    private async Task<IdentityApplicationFactory> CreateSsoOrganizationAndUserAsync(
        IdentityApplicationFactory factory,
        SsoConfigurationData ssoConfigurationData,
        string challenge,
        string testEmail,
        Guid? orgId = null,
        string orgTwoFactor = null,
        string userTwoFactor = null,
        Permissions permissions = null)
    {
        var authorizationCode = new AuthorizationCode
        {
            ClientId = "web",
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = "https://localhost:8080/sso-connector.html",
            RequestedScopes = ["api", "offline_access"],
            CodeChallenge = challenge.Sha256(),
            CodeChallengeMethod = "plain",
            Subject = null!, // Temporarily set it to null
        };

        factory.SubstituteService<IAuthorizationCodeStore>(service =>
        {
            service.GetAuthorizationCodeAsync("test_code")
                .Returns(authorizationCode);
        });

        var user = await factory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = testEmail,
                MasterPasswordHash = _testPassword,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel()
                {
                    PublicKey = Bit.Test.Common.Constants.TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring
                },
                UserSymmetricKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            });

        var userService = factory.GetService<IUserService>();
        if (userTwoFactor != null)
        {
            user.TwoFactorProviders = userTwoFactor;
            await userService.UpdateTwoFactorProviderAsync(user, TwoFactorProviderType.Email);
        }

        // Create Organization
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Id = orgId ?? Guid.NewGuid(),
            Name = "Test Org",
            BillingEmail = "billing-email@example.com",
            Plan = "Enterprise",
            UsePolicies = true,
            UseSso = true,
            Use2fa = !string.IsNullOrEmpty(userTwoFactor) || !string.IsNullOrEmpty(orgTwoFactor),
            TwoFactorProviders = orgTwoFactor,
        });

        if (orgTwoFactor != null)
        {
            factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("globalSettings:Duo:AKey", "WJHB374KM3N5hglO9hniwbkibg$789EfbhNyLpNq1");
            });
        }

        // Register User to Organization
        var organizationUserRepository = factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var orgUserPermissions =
            (permissions == null) ? null : JsonSerializer.Serialize(permissions, JsonHelpers.CamelCase);
        var organizationUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Permissions = orgUserPermissions
        });

        // Configure SSO
        var ssoConfigRepository = factory.Services.GetRequiredService<ISsoConfigRepository>();
        await ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(ssoConfigurationData, JsonHelpers.CamelCase),
        });

        var subject = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(JwtClaimTypes.Subject, user.Id.ToString()), // Get real user id
            new Claim(JwtClaimTypes.Name, testEmail),
            new Claim(JwtClaimTypes.IdentityProvider, "sso"),
            new Claim("organizationId", organization.Id.ToString()),
            new Claim(JwtClaimTypes.SessionId, "SOMETHING"),
            new Claim(JwtClaimTypes.AuthenticationMethod, "external"),
            new Claim(JwtClaimTypes.AuthenticationTime, new DateTimeOffset(DateTime.UtcNow.AddMinutes(-1)).ToUnixTimeSeconds().ToString())
        ], "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role));

        authorizationCode.Subject = subject;

        return factory;
    }
}
